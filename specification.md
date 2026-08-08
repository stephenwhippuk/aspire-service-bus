Aspire Service Bus

For use in DotNet Aspire, Microsoft provides a Service Bus EMulator. This however is incompatable with the Open Source Service Bus Explorer making it laboureous to send Service Bus messages into the emulator

The purpose of this project is a P.O.C for an aspire module that can be used to conveniently create messages to send to the app, accessible from the Aspire Dashboard once the apphost is up and running and has started up the Service Bus Emulator. 

for the purposes of this initial experiment. We will need

Asn Aspire Host Project, within wihich will be hosted aService which will read from the emulator and output messages it recieves to the console

A Service Bus EMulator (The Microsoft Aspire Module for this)

OUr Module which will host a simple UI allowing Headers and a Body to be provided and sent to the Service bus emulator. 
