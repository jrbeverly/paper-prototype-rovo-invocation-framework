terraform {
  required_version = ">= 1.5"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }

  # Uncomment and configure to use remote state:
  # backend "s3" {
  #   bucket         = "<state-bucket>"
  #   key            = "rovo-invocation-framework/terraform.tfstate"
  #   region         = "us-east-1"
  #   dynamodb_table = "<lock-table>"
  # }
}

provider "aws" {
  region = var.aws_region
}

resource "aws_iam_role" "invocation_handler" {
  name = "${var.name_prefix}-invocation-handler"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action    = "sts:AssumeRole"
      Effect    = "Allow"
      Principal = { Service = "lambda.amazonaws.com" }
    }]
  })
}

resource "aws_iam_role_policy_attachment" "invocation_handler_basic" {
  role       = aws_iam_role.invocation_handler.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

resource "aws_lambda_function" "invocation_handler" {
  filename         = "${path.module}/../build/invocation-handler.zip"
  function_name    = "${var.name_prefix}-invocation-handler"
  role             = aws_iam_role.invocation_handler.arn
  handler          = "InvocationHandler::InvocationHandler.Function::FunctionHandler"
  runtime          = "dotnet8"
  source_code_hash = filebase64sha256("${path.module}/../build/invocation-handler.zip")
  timeout          = 30

  environment {
    variables = {
      CONFLUENCE_BASE_URL          = var.confluence_base_url
      CONFLUENCE_USER_EMAIL        = var.confluence_user_email
      CONFLUENCE_API_TOKEN         = var.confluence_api_token
      CONFLUENCE_SPACE_KEY         = var.confluence_space_key
      CONFLUENCE_NAMESPACE_PAGE_ID = var.confluence_namespace_page_id
    }
  }
}

resource "aws_lambda_function" "poll_completion" {
  filename         = "${path.module}/../build/invocation-handler.zip"
  function_name    = "${var.name_prefix}-poll-completion"
  role             = aws_iam_role.invocation_handler.arn
  handler          = "InvocationHandler::InvocationHandler.PollCompletionFunction::FunctionHandler"
  runtime          = "dotnet8"
  source_code_hash = filebase64sha256("${path.module}/../build/invocation-handler.zip")
  timeout          = 30

  environment {
    variables = {
      CONFLUENCE_BASE_URL          = var.confluence_base_url
      CONFLUENCE_USER_EMAIL        = var.confluence_user_email
      CONFLUENCE_API_TOKEN         = var.confluence_api_token
      CONFLUENCE_SPACE_KEY         = var.confluence_space_key
      CONFLUENCE_NAMESPACE_PAGE_ID = var.confluence_namespace_page_id
    }
  }
}

resource "aws_lambda_function" "extract_result" {
  filename         = "${path.module}/../build/invocation-handler.zip"
  function_name    = "${var.name_prefix}-extract-result"
  role             = aws_iam_role.invocation_handler.arn
  handler          = "InvocationHandler::InvocationHandler.ExtractResultFunction::FunctionHandler"
  runtime          = "dotnet8"
  source_code_hash = filebase64sha256("${path.module}/../build/invocation-handler.zip")
  timeout          = 30

  environment {
    variables = {
      CONFLUENCE_BASE_URL          = var.confluence_base_url
      CONFLUENCE_USER_EMAIL        = var.confluence_user_email
      CONFLUENCE_API_TOKEN         = var.confluence_api_token
      CONFLUENCE_SPACE_KEY         = var.confluence_space_key
      CONFLUENCE_NAMESPACE_PAGE_ID = var.confluence_namespace_page_id
    }
  }
}

resource "aws_iam_role" "state_machine" {
  name = "${var.name_prefix}-state-machine"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action    = "sts:AssumeRole"
      Effect    = "Allow"
      Principal = { Service = "states.amazonaws.com" }
    }]
  })
}

resource "aws_iam_role_policy" "state_machine_invoke_lambda" {
  name = "invoke-lambda"
  role = aws_iam_role.state_machine.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Action = "lambda:InvokeFunction"
      Resource = [
        aws_lambda_function.poll_completion.arn,
        aws_lambda_function.extract_result.arn,
      ]
    }]
  })
}

resource "aws_sfn_state_machine" "completion_poller" {
  name     = "${var.name_prefix}-completion-poller"
  role_arn = aws_iam_role.state_machine.arn

  definition = jsonencode({
    Comment = "Polls Confluence until a Rovo agent response appears, then extracts the result."
    StartAt = "Initialize"
    States = {
      Initialize = {
        Type = "Pass"
        Parameters = {
          "RequestPageId.$" = "$.RequestPageId"
          PollAttempt       = 0
        }
        Next = "Wait"
      }
      Wait = {
        Type    = "Wait"
        Seconds = var.poll_interval_seconds
        Next    = "PollCompletion"
      }
      PollCompletion = {
        Type     = "Task"
        Resource = aws_lambda_function.poll_completion.arn
        Parameters = {
          "RequestPageId.$" = "$.RequestPageId"
        }
        ResultPath = "$.PollResult"
        Retry = [{
          ErrorEquals     = ["Lambda.ServiceException", "Lambda.AWSLambdaException", "Lambda.SdkClientException", "Lambda.TooManyRequestsException"]
          IntervalSeconds = 2
          MaxAttempts     = 3
          BackoffRate     = 2
        }]
        Next = "CheckCompletion"
      }
      CheckCompletion = {
        Type = "Choice"
        Choices = [{
          Variable      = "$.PollResult.IsComplete"
          BooleanEquals = true
          Next          = "ExtractResult"
        }]
        Default = "IncrementAttempt"
      }
      IncrementAttempt = {
        Type = "Pass"
        Parameters = {
          "RequestPageId.$" = "$.RequestPageId"
          "PollAttempt.$"   = "States.MathAdd($.PollAttempt, 1)"
          "PollResult.$"    = "$.PollResult"
        }
        Next = "CheckTimeout"
      }
      CheckTimeout = {
        Type = "Choice"
        Choices = [{
          Variable                 = "$.PollAttempt"
          NumericGreaterThanEquals = var.max_poll_attempts
          Next                     = "TimedOut"
        }]
        Default = "Wait"
      }
      ExtractResult = {
        Type     = "Task"
        Resource = aws_lambda_function.extract_result.arn
        Parameters = {
          "ResponsePageId.$" = "$.PollResult.ResponsePageId"
        }
        ResultPath = "$.Result"
        Retry = [{
          ErrorEquals     = ["Lambda.ServiceException", "Lambda.AWSLambdaException", "Lambda.SdkClientException", "Lambda.TooManyRequestsException"]
          IntervalSeconds = 2
          MaxAttempts     = 3
          BackoffRate     = 2
        }]
        Next = "ReturnResult"
      }
      ReturnResult = {
        Type       = "Pass"
        OutputPath = "$.Result"
        End        = true
      }
      TimedOut = {
        Type  = "Fail"
        Error = "TimedOut"
        Cause = "Rovo agent did not respond within the allotted polling window."
      }
    }
  })
}
