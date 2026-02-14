# GlassDFIR

GlassDFIR is a modern, feature-rich Windows Digital Forensics and Incident Response (DFIR) tool designed to streamline evidence collection, analysis, and timeline visualization. Built with WPF and .NET 9, it integrates powerful forensic tools into a unified, user-friendly interface.

![Dashboard](GlassDFIR\Assets\dashboard.png)

## 🚀 Features

### **Evidence Collection & Analysis**
- **Browser History**: Extract and unify history from Chrome, Edge, and Firefox. Includes automatic timestamp conversion to UTC.
- **User Activity**: Analyze UserAssist, JumpLists, and ShellBags.
- **File System**: Process $MFT, $J, $LogFile, $Boot, and $SDS using MFTECmd.
- **Registry Forensics**: Parse Amcache, ShimCache, and SYSTEM hives (RECmd).
- **Network Forensics**: Analyze SRUM data (SrumECmd) and Windows Timeline (WxTCmd).
- **Memory Forensics**: Integration with Volatility 3 for memory analysis.
- **Live Response**: Run tools like SBECmd and Hayabusa in live mode.

### **Visualization & Timeline**
- **Advanced CSV Viewer**: High-performance grid with filtering, sorting, column grouping, and distinct value analysis.
- **Interactive Histogram**: Visualize event distribution over time with zoom capabilities.
- **Query History**: Dedicated sub-blade for analyzing search queries extracted from browser history.
- **Theming**: Sleek "Tokyo Night" theme for reduced eye strain during long investigations.

![Timeline](GlassDFIR\Assets\timeline.png)

### **Tool Integration**
Seamlessly runs industry-standard tools:
- **Eric Zimmerman's Tools**: MFTECmd, LECmd, JLECmd, AmcacheParser, SrumECmd, WxTCmd, RECmd, SBECmd.
- **Hayabusa**: Fast forensic timeline generation and threat hunting.
- **YARA**: Scan files using custom or built-in YARA rules.

## 🛠️ Prerequisites

- **OS**: Windows 10/11 (64-bit)
- **Framework**: [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- **PowerShell**: Required for initial tool setup script.
- **Admin Privileges**: Required for accessing locked system files (e.g., Registry hives, $MFT).

## 📦 Setup & Installation

1.  **Clone the Repository**
    ```bash
    git clone https://github.com/yourusername/GlassDFIR.git
    cd GlassDFIR
    ```

2.  **Download Dependencies**
    GlassDFIR relies on several external tools. 
    
    The dashboard contains buttons that will download the tool on your behalf.

    *Note: This script downloads the required tools to a `Tools/` directory (ignored by git).*

3.  **Build and Run**
    You can run the application using the .NET CLI:
    ```bash
    dotnet run --project GlassDFIR
    ```
    Or open `GlassDFIR.sln` in Visual Studio 2022 and press `F5`.

## 📖 Usage

### **Dashboard**
The Dashboard serves as the command center.
- **Tool Runner**: Select a tool from the sidebar (e.g., "MFT Explorer", "Browser History").
- **Configuration**: Set input files/folders and output directories.
- **Execution**: Click "Run Tool". GlassDFIR handles the command-line execution and displays real-time logs.

### **Viewing Results**
Once a tool completes, the results are automatically loaded into a "Sub-Blade" (tab).
- **Grid View**: Filter by column, search globally, or group by specific fields.
- **Timeline**: Use the histogram at the top to filter events by time range.
- **Export**: Save filtered results to a new CSV for reporting.

### **Case Management**
GlassDFIR organizes outputs into "Cases".
- **Default Output**: `GlassDFIR_Output/` directory.
- **Evidence Collection**: Collect only the evidence you require with a click of a button.
- **Custom Cases**: Create and manage cases to keep evidence separated.
![alt text](GlassDFIR\Assets\case.png)

## ⚠️ Notes

- **Anti-Virus**: Some forensic tools (e.g., Mimikatz plugins in Volatility, or raw disk access tools) may trigger AV alerts. It is recommended to configure exclusions for the `Tools/` directory.
- **Elevation**: Always run GlassDFIR as **Administrator** when analyzing live system artifacts to ensure complete data collection.

## 📄 License

[MIT License](LICENSE)
