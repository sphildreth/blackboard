#!/bin/zsh
# Script to run the Blackboard BBS application from the console

# Navigate to the directory containing this script
cd "$(dirname "$0")"

# Build the application (optional, comment out if not needed)
dotnet build src/Blackboard/Blackboard.csproj

# Run the application
dotnet run --project src/Blackboard/Blackboard.csproj
