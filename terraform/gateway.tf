# References
# - https://github.com/turnerlabs/terraform-ecs-fargate-apigateway/tree/master/env/dev
# - https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-vs-rest.html
# - https://docs.aws.amazon.com/apigateway/latest/developerguide/apigateway-use-lambda-authorizer.html
# - https://docs.aws.amazon.com/apigateway/latest/developerguide/getting-started-with-private-integration.html

# REST over HTTP:
# - Rate limiting and throttling
# - Caching
# - Canary deployments
# - Monitoring: X-Ray tracing, excecution logs

# TODO
# - Create custom domain
# - Define models
# - Create gateway/stages/methods from OpenApi definition
# - Create custom authorizer
# - Add trigger to aws_api_gateway_deployment


resource "aws_api_gateway_rest_api" "todolist" {
  name = "todolist"
  endpoint_configuration {
    types = ["REGIONAL"]
  }
}

resource "aws_api_gateway_vpc_link" "todolist" {
  name        = "todolist"
  target_arns = [aws_lb.todolist.arn]
}

resource "aws_api_gateway_resource" "main" {
  rest_api_id = aws_api_gateway_rest_api.todolist.id
  parent_id   = aws_api_gateway_rest_api.todolist.root_resource_id
  path_part   = "{proxy+}"
}

resource "aws_api_gateway_method" "main" {
  rest_api_id      = aws_api_gateway_rest_api.todolist.id
  resource_id      = aws_api_gateway_resource.main.id
  authorization    = "CUSTOM"
  authorizer_id    = aws_api_gateway_authorizer.todolist.id
  http_method      = "ANY"
  api_key_required = false
  request_parameters = {
    "method.request.path.proxy"           = true
    "method.request.header.Authorization" = true
    "method.request.header.X-CustomToken" = true
  }
}

# Private Integration via VPC Link with Network LB
resource "aws_api_gateway_integration" "main" {
  rest_api_id             = aws_api_gateway_rest_api.todolist.id
  resource_id             = aws_api_gateway_resource.main.id
  http_method             = aws_api_gateway_method.main.http_method
  type                    = "HTTP_PROXY"
  integration_http_method = "ANY"
  connection_type         = "VPC_LINK" # should be VPC_LINK for private backend
  connection_id           = aws_api_gateway_vpc_link.todolist.id
  uri                     = "http://${aws_lb.todolist.dns_name}/{proxy}"
  timeout_milliseconds    = 29000 # 50-29000

  cache_key_parameters = ["method.request.path.proxy"]
  request_parameters = {
    "integration.request.path.proxy" = "method.request.path.proxy"
  }

}

# Changes to Gateway should be deployed every time
# https://docs.aws.amazon.com/apigateway/latest/developerguide/how-to-deploy-api.html
resource "aws_api_gateway_deployment" "main" {
  depends_on  = [aws_api_gateway_integration.main]
  rest_api_id = aws_api_gateway_rest_api.todolist.id
}

resource "aws_api_gateway_stage" "todolist_v1" {
  deployment_id        = aws_api_gateway_deployment.main.id
  rest_api_id          = aws_api_gateway_rest_api.todolist.id
  stage_name           = "v1"
  xray_tracing_enabled = true
  access_log_settings {
    destination_arn = aws_cloudwatch_log_group.gateway_todolist.arn
    format = jsonencode({
      "requestId" : "$context.requestId",
      "extendedRequestId" : "$context.extendedRequestId",
      "ip" : "$context.identity.sourceIp",
      "caller" : "$context.identity.caller",
      "cognitoIdentityId" : "$context.identity.cognitoIdentityId",
      "user" : "$context.identity.user",
      "userAgent" : "$context.identity.userAgent",
      "requestTime" : "$context.requestTime",
      "httpMethod" : "$context.httpMethod",
      "resourcePath" : "$context.resourcePath",
      "apiKeyId" : "$context.identity.apiKeyId",
      "status" : "$context.status",
      "protocol" : "$context.protocol",
      "path" : "$context.path",
      "responseLength" : "$context.responseLength",
      "errorMessage" : "$context.error.message",
      "authorizerPrincipalId" : "$context.authorizer.principalId",
      "responseLatency" : "$context.responseLatency",
      "requestBody" : "$input.body"
    })

  }
}

resource "aws_api_gateway_method_settings" "all" {
  rest_api_id = aws_api_gateway_rest_api.todolist.id
  stage_name  = aws_api_gateway_stage.todolist_v1.stage_name
  method_path = "*/*"

  settings {
    metrics_enabled    = true
    logging_level      = "INFO" # Log Group: API-Gateway-Execution-Logs_{rest-api-id}/{stage_name}
    data_trace_enabled = true   # Full Request and Response Logs
  }
}

# Cloudwatch
resource "aws_cloudwatch_log_group" "gateway_todolist" {
  name              = "/aws/api-gateway/todolist"
  retention_in_days = 7
}

# AWS would create the log group by default with a 'Never expire' retention policy
resource "aws_cloudwatch_log_group" "api_gateway_execution_logs" {
  name              = "API-Gateway-Execution-Logs_${aws_api_gateway_rest_api.todolist.id}/${aws_api_gateway_stage.todolist_v1.stage_name}"
  retention_in_days = "7"
}

# AWS Xray
resource "aws_xray_sampling_rule" "todolist_api_sampling_rule" {
  rule_name      = "todolist-api-gateway"
  priority       = 9900
  version        = 1
  reservoir_size = 1
  fixed_rate     = 0.05
  http_method    = "*"
  host           = "*"
  url_path       = "*"
  service_name   = "todolist/*" # <api_name>/<stage_name>
  service_type   = "*"
  resource_arn   = "*"
  attributes     = {}
}

# Lambda Authorizer (global, part of common infrastructure)
data "aws_lambda_function" "authorizer" {
  function_name = "api-gateway-custom-authorizer"
}

resource "aws_api_gateway_authorizer" "todolist" {
  name                             = "todolist"
  rest_api_id                      = aws_api_gateway_rest_api.todolist.id
  authorizer_uri                   = data.aws_lambda_function.authorizer.invoke_arn
  type                             = "REQUEST"
  identity_source                  = "method.request.header.Authorization,method.request.header.X-CustomToken"
  authorizer_result_ttl_in_seconds = 300 # cached for 5 min
  authorizer_credentials           = aws_iam_role.invocation_role.arn
}

data "aws_iam_policy_document" "invocation_assume_role" {
  statement {
    effect = "Allow"

    principals {
      type        = "Service"
      identifiers = ["apigateway.amazonaws.com"]
    }

    actions = ["sts:AssumeRole"]
  }
}

resource "aws_iam_role" "invocation_role" {
  name               = "api_gateway_auth_invocation"
  path               = "/"
  assume_role_policy = data.aws_iam_policy_document.invocation_assume_role.json
}

data "aws_iam_policy_document" "invocation_policy" {
  statement {
    effect    = "Allow"
    actions   = ["lambda:InvokeFunction"]
    resources = [data.aws_lambda_function.authorizer.arn]
  }
}

resource "aws_iam_role_policy" "invocation_policy" {
  name   = "default"
  role   = aws_iam_role.invocation_role.id
  policy = data.aws_iam_policy_document.invocation_policy.json
}

# The API Gateway endpoint
output "api_gateway_endpoint" {
  value = aws_api_gateway_stage.todolist_v1.invoke_url
}
