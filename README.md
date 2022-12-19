# Nodes Sdk Sample project

Example project demonstrating how to connect to NODES marketplace using 
the DotNet SDK for DotNet.  

Also provides some sample use cases for: 
 - FSPs (Flexibility Service Providers): Registering assets and asset portfolios, placing sell orders
 - DSOs (Distributed System Operators): Creating grid topologies, approving assets, placing buy orders

Requires DotNet Core 6 or later.

# How to run the project


1) Get a client-id and corresponding client-secret from your Nodes contacts

2) Create a file application.local.json and put the values there, the file should  
   like this: 
   
   {
     "Authentication": {
       "ClientId": "{YourClientId}",
       "ClientSecret": "{YourClientSecret}"
     }
   }
   
3) Compile and run the project. By default, the program will first show some 
   usage information and then try to show information about the currently 
   logged in user, based on the provided client-id and client-secret.  
   This last step will fail unless the provided client id and secret are valid.  
    