provider "aws" {
  region = "eu-central-1"
}

module "WebSocket_VPC" {
  source  = "terraform-aws-modules/vpc/aws"
  version = "5.19.0"

  name = "WebSocket-VPC"
  cidr = "10.0.0.0/16"

  azs = ["eu-central-1a", "eu-central-1b"]

  public_subnets = ["10.0.101.0/24", "10.0.102.0/24"]

  private_subnets = []

  enable_nat_gateway = false
  enable_vpn_gateway = false

  enable_dns_hostnames = true
  enable_dns_support   = true
}

module "WebSocket_SG" {
  source  = "terraform-aws-modules/security-group/aws"
  version = "5.3.0"

  name        = "WebSocket-sg"
  description = "Security group for WebSocket Server EC2 instance"
  vpc_id      = module.WebSocket_VPC.vpc_id

  ingress_with_cidr_blocks = [
    {
      from_port   = 22
      to_port     = 22
      protocol    = "tcp"
      cidr_blocks = local.my_current_ip
      description = "SSH from my IP"
    },
    {
      from_port   = 80
      to_port     = 80
      protocol    = "tcp"
      cidr_blocks = "0.0.0.0/0"
      description = "HTTP from anywhere"
    },
    {
      from_port   = 443
      to_port     = 443
      protocol    = "tcp"
      cidr_blocks = "0.0.0.0/0"
      description = "HTTPS from anywhere"
    }
  ]

  egress_with_cidr_blocks = [
    {
      from_port   = 0
      to_port     = 0
      protocol    = "-1"
      cidr_blocks = "0.0.0.0/0"
      description = "Allow all outbound"
    }
  ]
}

resource "aws_instance" "app_server" {
  ami           = data.aws_ami.ubuntu.id
  instance_type = var.instance_type
  key_name      = var.key_name

  vpc_security_group_ids      = [module.WebSocket_SG.security_group_id]
  subnet_id                   = module.WebSocket_VPC.public_subnets[0]
  associate_public_ip_address = true

  user_data = file("${path.module}/scripts/install.sh")

  tags = {
    Name = var.instance_name
  }
}