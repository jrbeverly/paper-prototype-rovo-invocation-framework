variable "aws_region" {
  description = "AWS region to deploy resources into"
  type        = string
  default     = "us-east-1"
}

variable "name_prefix" {
  description = "Prefix applied to all resource names"
  type        = string
  default     = "rovo-invocation"
}

variable "confluence_base_url" {
  description = "Atlassian site base URL (e.g. https://yourorg.atlassian.net)"
  type        = string
}

variable "confluence_user_email" {
  description = "Email address used for Confluence basic auth"
  type        = string
}

variable "confluence_api_token" {
  description = "Atlassian API token for Confluence basic auth"
  type        = string
  sensitive   = true
}

variable "confluence_space_key" {
  description = "Confluence space key where request pages are created"
  type        = string
}

variable "confluence_namespace_page_id" {
  description = "Page ID of the parent namespace page for request pages"
  type        = string
}

variable "poll_interval_seconds" {
  description = "Seconds to wait between completion polls"
  type        = number
  default     = 30
}

variable "max_poll_attempts" {
  description = "Maximum number of poll attempts before the state machine times out"
  type        = number
  default     = 20
}
