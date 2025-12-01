output "lambda_function_name" {
  description = "Name of the deployed Lambda function"
  value       = aws_lambda_function.invocation_handler.function_name
}

output "lambda_function_arn" {
  description = "ARN of the deployed Lambda function"
  value       = aws_lambda_function.invocation_handler.arn
}

output "state_machine_arn" {
  description = "ARN of the completion poller state machine"
  value       = aws_sfn_state_machine.completion_poller.arn
}
