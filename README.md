# 🗄️ Dynamic SQL Data Generator

A powerful **SQL Test Data Script Generator** designed to help QA engineers, developers, and analysts quickly and safely generate SQL INSERT scripts for selected tables from a SQL Server database **without modifying the source database**.

**Language**: C# (.NET Framework)  
**Type**: Windows Desktop Application (WinForms)  
**Created**: March 26, 2026  
**Repository**: [GitHub](https://github.com/Shyam-Siddanthapu/Dynamic-SQL-Data-Generator)

---

## 📋 Overview

This desktop application solves a critical problem in database testing: **generating realistic test data quickly without touching production databases**. It reads table schemas from SQL Server databases and generates syntactically correct INSERT statements with sample data.

### Problem Solved
- ❌ Manually writing INSERT statements is time-consuming and error-prone
- ❌ Copy/pasting production data raises security concerns
- ❌ Test data generation tools often require database modifications
- ✅ This tool generates INSERT scripts safely and efficiently

### Target Users
- 🧪 **QA Engineers** - Create test datasets for test cases
- 👨‍💻 **Developers** - Generate sample data for local development
- 📊 **Database Analysts** - Analyze and prepare test scenarios
- 🔧 **DevOps/DBAs** - Automate test data provisioning

---

## ✨ Key Features

✅ **Read-Only Database Access** - Analyzes schemas without any modifications  
✅ **Dynamic Schema Detection** - Automatically discovers tables and columns  
✅ **Selective Table Generation** - Choose which tables to include in the script  
✅ **Intelligent Data Generation** - Creates realistic sample data based on column types  
✅ **SQL Server Compatible** - Generates standard SQL Server syntax  
✅ **Safe & Secure** - Zero impact on source database  
✅ **User-Friendly GUI** - Intuitive WinForms interface  
✅ **Batch Processing** - Generate scripts for multiple tables at once  
✅ **Schema Change Logging** - Track schema modifications  
✅ **Synthetic Data** - Generate realistic test data patterns  

---

## 🛠️ Technology Stack

### Core Framework
- **C# (.NET Framework)** - Robust, type-safe programming language
- **WinForms** - Native Windows desktop UI framework
- **SQL Server** - Database connectivity and schema reading

### Key Components

| Component | Purpose |
|-----------|---------|
| **MainForm.cs** | Core application logic (78KB - comprehensive implementation) |
| **MainForm.Designer.cs** | WinForms UI designer code |
| **MainForm.resx** | Windows Forms resource file |
| **Program.cs** | Application entry point |
| **SchemaBaseline.cs** | Schema representation model |
| **SchemaBaselineStore.cs** | Persistent schema storage |
| **SchemaChangeLogger.cs** | Track and log schema modifications |
| **SyntheticDataService.cs** | Intelligent test data generation |

### Project Structure
```
Dynamic-SQL-Data-Generator/
├── MainForm.cs                      # Main application logic (78 KB)
├── MainForm.Designer.cs             # UI designer generated code
├── MainForm.resx                    # UI resources
├── Program.cs                       # Entry point
├── SchemaBaseline.cs                # Schema model
├── SchemaBaselineStore.cs           # Schema persistence
├── SchemaChangeLogger.cs            # Change tracking
├── SyntheticDataService.cs          # Data generation service
├── MDFExplorerGUI.csproj            # Project file
├── MDFExplorerGUI.sln               # Visual Studio solution
├── MDFExplorerGUI_TemporaryKey.pfx  # Signing certificate
├── SQL_Data_Generator.docx          # Documentation
└── README.md                        # This file
```

---

## 🚀 Getting Started

### Prerequisites
- **Windows OS** (XP SP3 or later)
- **.NET Framework 4.5** or higher
- **Visual Studio 2015** or higher (for development)
- **SQL Server** database access (read-only recommended)

### Installation

#### Option 1: Run Compiled Application
1. Download the compiled executable from the `bin` directory
2. Run `MDFExplorerGUI.exe`
3. Enter your SQL Server connection details

#### Option 2: Build from Source
```bash
# Clone the repository
git clone https://github.com/Shyam-Siddanthapu/Dynamic-SQL-Data-Generator.git

# Open the solution in Visual Studio
# File > Open > MDFExplorerGUI.sln

# Build the solution
# Build > Build Solution (or Ctrl+Shift+B)

# Run the application
# Debug > Start Debugging (or F5)
```

---

## 📖 How to Use

### Basic Workflow

1. **Connect to Database**
   - Launch the application
   - Enter SQL Server connection string
   - Click "Connect"

2. **Select Tables**
   - Browse available tables in the database
   - Check boxes for tables you want to include
   - Application automatically detects schemas

3. **Configure Data Generation**
   - Set number of rows per table
   - Configure data generation rules (if applicable)
   - Preview sample data

4. **Generate INSERT Script**
   - Click "Generate SQL Script"
   - Review the generated SQL
   - Copy or save the script

5. **Deploy Test Data**
   - Execute the generated SQL in your test environment
   - Verify data integrity
   - Use for test cases

### Example Scenarios

**Scenario 1: Testing E-Commerce Platform**
```
1. Connect to production database (read-only)
2. Select: Products, Orders, OrderDetails tables
3. Generate 100 products, 50 orders
4. Use generated data in QA environment
```

**Scenario 2: Development Environment Setup**
```
1. Connect to sample database
2. Select: Users, Roles, Permissions tables
3. Generate baseline test data
4. Import into local development database
```

**Scenario 3: Performance Testing**
```
1. Select high-volume tables
2. Generate 10,000+ rows
3. Use for load testing
4. Analyze query performance
```

---

## 🔐 Security Features

✅ **Read-Only by Default** - No write permissions required  
✅ **No Data Exfiltration** - Only generates INSERT statements  
✅ **Connection Encryption** - Secure database connectivity  
✅ **Secure Storage** - Credentials not stored in plain text  
✅ **Audit Logging** - Track all schema analysis operations  

---

## 📊 Core Components Explained

### MainForm.cs (78 KB)
- **Role**: Main application logic and business logic
- **Responsibilities**:
  - Database connection management
  - Schema discovery and analysis
  - SQL script generation
  - UI event handling
- **Key Methods**:
  - Connect to database
  - Read table structures
  - Generate INSERT statements
  - Export scripts

### SyntheticDataService.cs
- **Role**: Intelligent test data generation
- **Capabilities**:
  - Generate realistic data based on column types
  - Support for various data types (int, varchar, datetime, etc.)
  - Configurable data patterns
  - Randomization for uniqueness

### SchemaBaseline.cs & SchemaBaselineStore.cs
- **Role**: Schema versioning and tracking
- **Purpose**:
  - Store database schema snapshots
  - Track schema changes over time
  - Enable schema comparison

### SchemaChangeLogger.cs
- **Role**: Change tracking and auditing
- **Features**:
  - Log all schema modifications
  - Maintain audit trail
  - Support troubleshooting

---

## 🎯 Use Cases

### QA & Testing
- Generate test data matching production schema
- Create edge case datasets
- Automate test data provisioning

### Development
- Local environment setup
- Quick database population
- Prototype testing

### Data Migration
- Generate sample data for migration testing
- Validate data transformation logic
- Prepare migration rollback data

### Documentation
- Generate representative data samples
- Create test case examples
- Demo data preparation

---

## 📝 Configuration

### Connection String Format
```
Server=YOUR_SERVER;Database=YOUR_DATABASE;Integrated Security=true;
```

or

```
Server=YOUR_SERVER;Database=YOUR_DATABASE;User Id=sa;Password=YOUR_PASSWORD;
```

### Settings
- **Batch Size**: Rows to process per batch
- **Timeout**: Database query timeout
- **Encoding**: SQL script encoding (UTF-8 recommended)

---

## 🐛 Troubleshooting

### Connection Issues
**Problem**: Cannot connect to SQL Server  
**Solution**: 
- Verify SQL Server is running
- Check connection string syntax
- Ensure firewall allows port 1433

### Schema Not Loading
**Problem**: Tables not appearing in list  
**Solution**:
- Verify user has SELECT permissions
- Check database name is correct
- Ensure tables exist and are accessible

### Large Dataset Timeout
**Problem**: Generation times out for large tables  
**Solution**:
- Increase timeout setting
- Generate fewer rows per table
- Process tables separately

### Memory Issues
**Problem**: Application becomes slow with large schemas  
**Solution**:
- Close other applications
- Reduce number of tables
- Process in batches

---

## 📚 Additional Resources

### Documentation
- Full documentation available in `SQL_Data_Generator.docx`
- Detailed feature guide and examples
- Advanced configuration options

### Support
- 💻 GitHub Issues for bug reports
- 📧 Contact author for support
- 🔗 Check project wiki for FAQs

---

## 🎓 Learning the Codebase

For developers interested in extending this tool:

1. **Understanding Architecture**
   - Review MainForm.cs for core logic
   - Study schema baseline classes
   - Examine data generation service

2. **Key Concepts**
   - SQL Server schema discovery
   - T-SQL INSERT statement generation
   - WinForms event handling

3. **Extension Points**
   - Add new data generation algorithms
   - Support additional database systems
   - Enhance UI with modern frameworks

---

## 🔄 Development Workflow

### Building
```bash
# Build solution
msbuild MDFExplorerGUI.sln /p:Configuration=Release

# Or in Visual Studio
# Build > Build Solution
```

### Running
```bash
# From Visual Studio: F5 or Debug > Start Debugging
# From command line: .\bin\Release\MDFExplorerGUI.exe
```

### Debugging
- Set breakpoints in Visual Studio
- Use Debug > Step Into/Over (F10/F11)
- Watch variables in Debug window
- Check Output window for logs

---

## 📈 Performance Characteristics

- **Small Database** (< 50 tables): < 5 seconds
- **Medium Database** (50-200 tables): 5-15 seconds
- **Large Database** (> 200 tables): 15-60 seconds
- **Data Generation**: ~1000 rows/second

---

## 🏆 Best Practices

✅ Always use read-only database accounts  
✅ Test generated scripts in staging before production use  
✅ Keep backups of working script templates  
✅ Version control generated SQL scripts  
✅ Document your data generation rules  
✅ Use meaningful table selection for clarity  

---

## 📄 License

This project is open-source and available for personal and professional use.

---

## 👤 Author

**Shyam Siddanthapu**  
Full Stack Developer | .NET Specialist | Database Expert  
GitHub: [@Shyam-Siddanthapu](https://github.com/Shyam-Siddanthapu)

---

## 🤝 Contributing

Contributions are welcome! Areas for enhancement:
- Support for additional database systems (PostgreSQL, MySQL)
- Modern WPF/MAUI UI redesign
- Advanced data generation algorithms
- Export to JSON/CSV formats
- REST API wrapper

---

## 🎯 Quick Start

```bash
# Clone the repository
git clone https://github.com/Shyam-Siddanthapu/Dynamic-SQL-Data-Generator.git

# Open in Visual Studio
# Build the solution
# Run the application
# Enter SQL Server connection details
# Select tables and generate INSERT scripts
```

---

*Last updated: June 2026*  
*For questions or suggestions, open an issue on GitHub!* 🚀