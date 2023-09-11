terraform {
  required_version = "~> 1.3"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 4.56"
    }
  }
}

provider "aws" {
  region  = local.aws_region
  profile = local.aws_profile

  default_tags {
    tags = { name = "todolist" }
  }

}

locals {
  aws_account = "397413993642"                        # AWS account
  aws_region  = "eu-west-1"                           # AWS region
  aws_profile = "alto-sandbox-developer-397413993642" # AWS profile

  ecr_reg   = "${local.aws_account}.dkr.ecr.${local.aws_region}.amazonaws.com" # ECR docker registry URI
  ecr_repo  = "todolist"                                                       # ECR repo name
  image_tag = "latest"

  logs_group = "/ecs/todolist"
}

