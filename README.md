
# JCTG Project Overview

The JCTG project is structured into three major components, each serving a unique role in the overall functionality of the system. These components are designed to work together to capture TradingView requests, process them, and interact with Metatrader applications.

## Components

### 1. JCTG.AzureFunction

- **Description**: This component is designed to be hosted as an Azure Function. Its primary role is to capture requests from TradingView.
- **Functionality**:
  - Acts as a serverless function in Azure.
  - Processes incoming HTTP requests from TradingView.
  - Parses and forwards the requests to the JCTG.Client component for further processing.

### 2. JCTG.Client

- **Description**: This is a Console application that runs on a Virtual Machine (VM) where the Metatrader applications are installed.
- **Functionality**:
  - Listens for data from the JCTG.AzureFunction.
  - Interprets the data and interacts with the Metatrader applications accordingly.
  - Can be configured to work with multiple instances of Metatrader.

### 3. JCTG.MQL

- **Description**: This component comprises the source code of the Expert Advisors (EAs) that are to be installed in Metatrader 4 or 5.
- **Functionality**:
  - Contains the MQL (MetaQuotes Language) scripts for Expert Advisors.
  - These scripts are responsible for executing trades and managing them based on the signals received from the JCTG.Client.
  - Compatible with both Metatrader 4 and Metatrader 5 platforms.

## Installation and Setup

Each component requires specific setup procedures:

1. **JCTG.AzureFunction**: Deploy the function to Azure and configure it to receive requests from TradingView.
2. **JCTG.Client**: Install the console application on a VM where Metatrader is running. Ensure it's configured to communicate with the Azure Function.
3. **JCTG.MQL**: Install the Expert Advisors in Metatrader by placing the MQL scripts in the appropriate directory of your Metatrader installation.

## Conclusion

The JCTG project is a comprehensive solution that integrates cloud functionality with trading applications. By leveraging Azure Functions, console applications, and custom MQL scripts, it offers a robust platform for automated trading strategies.

---

Copyright © 2023 Joeri Pansaerts

For more information, visit [Just Call The Guy](https://www.justcalltheguy.io).
