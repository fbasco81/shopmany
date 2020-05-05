# Introduction 
This solution is made by a dotnet core 3.1 api project, plus a compose file that contains the dotentcore api, jaeger and datadog agent
Gaol of this solutionis to show how to usen opentracing dotnet core nuget package, and how to plug datadog or jaeger behind it (similar to use serilog and see how to switch between target sinks at runtime)
# Getting Started
This sample requires docker installed and running on your machine.
You can run the compose within visual studio (make sure the docker-compose project is set as startup project), or running docker-compose up in the directory where the docker-compose.yaml file is.
Running in visual studio will let you debug the application
See docker-compose.yaml to learn how to target Jaeger or datadog as sink for traces
See docker-compose.yaml for explanations on env varibales for Datadog and Jaeger


