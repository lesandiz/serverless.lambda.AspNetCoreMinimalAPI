data "aws_iam_role" "ecs_task_exec_role" {
  name = "ecs_task_exec_role"
}

data "aws_iam_role" "ecs_task_role" {
  name = "ecs_task_role"
}

resource "aws_ecs_cluster" "todolist" {
  name = "todolist"

  setting {
    name  = "containerInsights"
    value = "enabled"
  }
}

resource "aws_ecs_cluster_capacity_providers" "todolist" {
  cluster_name = aws_ecs_cluster.todolist.name

  capacity_providers = [
    "FARGATE",
    "FARGATE_SPOT",
  ]

  default_capacity_provider_strategy {
    base              = 1
    weight            = 100
    capacity_provider = "FARGATE"
  }
}

resource "aws_lb" "todolist" {
  name               = "todolist-alb-internal"
  internal           = true
  load_balancer_type = "network" # Required for VPC Link
  subnets            = data.aws_subnets.todolist_private.ids
  #security_groups    = data.aws_security_groups.todolist.ids
}

resource "aws_lb_target_group" "todolist" {
  name        = "todolist-target-group"
  port        = 80
  protocol    = "TCP"
  target_type = "ip"
  vpc_id      = data.aws_vpc.todolist.id
  health_check {
    path                = "/welcome"
    port                = 80
    healthy_threshold   = 2
    unhealthy_threshold = 2
    interval            = 15
    timeout             = 5
  }
}

resource "aws_lb_listener" "todolist" {
  load_balancer_arn = aws_lb.todolist.arn
  port              = "80"
  protocol          = "TCP"

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.todolist.arn
  }
}

resource "aws_cloudwatch_log_group" "ecs_todolist" {
  name              = local.logs_group
  retention_in_days = 7
}

resource "aws_ecs_task_definition" "todolist" {
  family                   = "todolist"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = 1024
  memory                   = 3072
  execution_role_arn       = data.aws_iam_role.ecs_task_exec_role.arn
  task_role_arn            = data.aws_iam_role.ecs_task_role.arn
  container_definitions = jsonencode([
    {
      name      = "todolist"
      image     = "397413993642.dkr.ecr.eu-west-1.amazonaws.com/todolist:latest"
      cpu       = 10
      memory    = 512
      essential = true
      portMappings = [
        {
          containerPort = 443
          hostPort      = 443
        },
        {
          containerPort = 80
          hostPort      = 80
        }
      ],
      logConfiguration = {
        logDriver = "awslogs",
        options = {
          awslogs-group         = "${local.logs_group}",
          awslogs-region        = "${local.aws_region}",
          awslogs-stream-prefix = "ecs"
        }
      }
    },
    {
      name      = "aws-otel-collector"
      image     = "amazon/aws-otel-collector"
      cpu       = 10
      memory    = 256
      essential = true,
      command = [
        "--config=/etc/ecs/ecs-default-config.yaml",
        "--set=service.telemetry.logs.level=DEBUG"
      ],
      logConfiguration = {
        logDriver = "awslogs",
        options = {
          awslogs-group         = "${local.logs_group}",
          awslogs-region        = "${local.aws_region}",
          awslogs-stream-prefix = "ecs"
        }
      },
      healthCheck = {
        command = [
          "/healthcheck"
        ],
        interval    = 30,
        timeout     = 5,
        retries     = 3,
        startPeriod = 1
      }
    }
  ])
  runtime_platform {
    operating_system_family = "LINUX"
    cpu_architecture        = "X86_64"
  }
}

resource "aws_ecs_service" "todolist" {
  name                  = "todolist"
  cluster               = aws_ecs_cluster.todolist.id
  task_definition       = aws_ecs_task_definition.todolist.arn
  desired_count         = 1
  force_new_deployment  = true
  wait_for_steady_state = true
  launch_type           = "FARGATE"

  load_balancer {
    target_group_arn = aws_lb_target_group.todolist.arn
    container_name   = "todolist"
    container_port   = 80
  }

  network_configuration {
    subnets          = data.aws_subnets.todolist.ids
    security_groups  = data.aws_security_groups.todolist.ids # optional
    assign_public_ip = true                                  # optional
  }
}
