param(
    [Parameter(Mandatory = $true)]
    [string]$RoleArn,
    
    [Parameter(Mandatory = $false)]
    [string]$Profile
)

# Execute the AWS CLI command and capture the output
$awsCliCommand = "aws sts assume-role --role-arn $RoleArn --role-session-name my-sls-session --out json"
if ($Profile) {
    $awsCliCommand += " --profile $Profile"
}

$CREDS = Invoke-Expression -Command $awsCliCommand

# Extract the necessary credentials from the JSON output
$AWS_ACCESS_KEY_ID = ($CREDS | ConvertFrom-Json).Credentials.AccessKeyId
$AWS_SECRET_ACCESS_KEY = ($CREDS | ConvertFrom-Json).Credentials.SecretAccessKey
$AWS_SESSION_TOKEN = ($CREDS | ConvertFrom-Json).Credentials.SessionToken

# Set the environment variables for the AWS credentials
$env:AWS_ACCESS_KEY_ID = $AWS_ACCESS_KEY_ID
$env:AWS_SECRET_ACCESS_KEY = $AWS_SECRET_ACCESS_KEY
$env:AWS_SESSION_TOKEN = $AWS_SESSION_TOKEN