resource "aws_api_gateway_rest_api" "todolist" {
  name = "todolist"
  endpoint_configuration {
    types = ["REGIONAL"]
  }
}

resource "aws_api_gateway_resource" "main" {
  rest_api_id = aws_api_gateway_rest_api.todolist.id
  parent_id   = aws_api_gateway_rest_api.todolist.root_resource_id
  path_part   = "{proxy+}"
}

resource "aws_api_gateway_method" "main" {
  rest_api_id      = aws_api_gateway_rest_api.todolist.id
  resource_id      = aws_api_gateway_resource.main.id
  authorization    = "NONE"
  http_method      = "ANY"
  api_key_required = false
  request_parameters = {
    "method.request.path.proxy" = true
  }
}

resource "aws_api_gateway_integration" "main" {
  rest_api_id             = aws_api_gateway_rest_api.todolist.id
  resource_id             = aws_api_gateway_resource.main.id
  http_method             = aws_api_gateway_method.main.http_method
  type                    = "HTTP_PROXY"
  integration_http_method = "ANY"
  connection_type         = "INTERNET" # should be VCP_LINK for private backend
  uri                     = "http://${aws_alb.todolist.dns_name}/{proxy}"
  timeout_milliseconds    = 29000 # 50-29000

  cache_key_parameters = ["method.request.path.proxy"]
  request_parameters = {
    "integration.request.path.proxy" = "method.request.path.proxy"
  }

}

resource "aws_api_gateway_deployment" "main" {
  depends_on  = [aws_api_gateway_integration.main]
  rest_api_id = aws_api_gateway_rest_api.todolist.id
}

resource "aws_api_gateway_stage" "todolist" {
  deployment_id = aws_api_gateway_deployment.main.id
  rest_api_id   = aws_api_gateway_rest_api.todolist.id
  stage_name    = "v1"
  xray_tracing_enabled = true
}

# Cloudwatch
resource "aws_cloudwatch_log_group" "gateway_todolist" {
  name              = "/aws/api-gateway/todolist"
  retention_in_days = 7
}

# AWS Xray
resource "aws_xray_sampling_rule" "todolist_api_sampling_rule" {
  rule_name      = "todolist"
  priority       = 1
  version        = 1
  reservoir_size = 0
  fixed_rate     = 0
  http_method    = "*"
  host           = "*"
  url_path       = "*"
  service_name   = "todolist"
  service_type   = "*"
  resource_arn   = "*"
  attributes     = {}
}

//The API Gateway endpoint
output "api_gateway_endpoint" {
  value = aws_api_gateway_stage.todolist.invoke_url
}
