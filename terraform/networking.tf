data "aws_vpc" "todolist" {
  filter {
    name   = "tag:Name"
    values = ["todolist-vpc"]
  }
}

data "aws_subnets" "todolist" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.todolist.id]
  }
}

data "aws_subnets" "todolist_public" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.todolist.id]
  }

  filter {
    name   = "tag:Name"
    values = ["*public*"]
  }
}

data "aws_security_groups" "todolist" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.todolist.id]
  }
}
