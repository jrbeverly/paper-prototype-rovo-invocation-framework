.DEFAULT_GOAL := help

.PHONY: help doctor validate build plan deploy

help: ## Show available targets
	@awk 'BEGIN {FS = ":.*?## "} /^[a-zA-Z_-]+:.*?## / {printf "  %-10s %s\n", $$1, $$2}' $(MAKEFILE_LIST)

doctor: ## Verify required tools are installed
	@command -v dotnet    >/dev/null || (echo "ERROR: dotnet not found";    exit 1)
	@command -v terraform >/dev/null || (echo "ERROR: terraform not found"; exit 1)
	@command -v aws       >/dev/null || (echo "ERROR: aws not found";       exit 1)
	@command -v zip       >/dev/null || (echo "ERROR: zip not found";       exit 1)
	@echo "All required tools found."

validate: ## Validate Terraform configuration and source code
	terraform fmt -check infra/
	@mkdir -p build && touch build/invocation-handler.zip
	terraform -chdir=infra init -backend=false -input=false -reconfigure
	terraform -chdir=infra validate
	dotnet build src/

build: ## Build and package Lambda functions
	dotnet publish src/InvocationHandler/ -c Release -r linux-x64 --self-contained false -o build/lambda
	rm -f build/invocation-handler.zip
	cd build/lambda && zip -r ../invocation-handler.zip .

plan: build ## Preview infrastructure changes (requires AWS credentials)
	terraform -chdir=infra init -input=false
	terraform -chdir=infra plan

deploy: build ## Deploy infrastructure to AWS (requires AWS credentials)
	terraform -chdir=infra init -input=false
	terraform -chdir=infra apply
