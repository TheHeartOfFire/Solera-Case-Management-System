# Solera Case Management Tool (SCMT)

SCMT is an enterprise-grade C# desktop workbench designed to abstract, parse, and manipulate proprietary `.formgen` configuration architectures. Built completely from scratch, the platform optimizes workflow ergonomics by integrating automated file-structure manipulation, an extensible code-snippet engine, and programmatic template generation.

## 🛠️ Architecture & Core Dependencies

The application strictly implements production-grade patterns to ensure a clean separation of concerns, strict decoupling, and high testability.

* **UI & Presentation Layer:** Built with **WPF (.NET 9 / C# 13)** utilizing the **WPF UI (WPF-UI)** library for modern, native component styling.
* **Design Pattern:** Architecture is driven by the **MVVM (Model-View-ViewModel)** pattern via the `CommunityToolkit.Mvvm` library.
* **Inversion of Control:** Implements full **Dependency Injection (DI)** managed through `Microsoft.Extensions.Hosting` to isolate component lifecycles.
* **Diagnostics:** Configured with `Serilog` to provide robust, structured logging across internal processes.
* **DevOps Lifecycle:** Leverages `Velopack` for compiling delta packages, building seamless installers, and handling silent background application updates.

## 🚀 Key Engineering Focuses

### 1. Advanced Configuration Parsing (.formgen Utilities)
* Engineered an internal structure editor capable of deserializing, editing, and validating proprietary `.formgen` file layouts.
* Allows direct, automated memory-mapping and structural updates to file schemas, including runtime manipulation of property keys, system settings, and internal UUID allocations.

### 2. Form Compliance & Syntax Generation
* Built a standardized naming engine that algorithmically enforces strict compliance standards across development environments.
* Integrates a local code repository and dynamic text-token template subsystem to instantly assemble system commands, production notes, database-ready case summaries, and administrative communications.

### 3. Desktop DevSecOps Pipeline
* Configured automated build targets utilizing the GitHub CLI (`gh`) to coordinate production cycles.
* On execution, the build targets compile the binaries in `Release` mode, delegate installer packaging to Velopack, auto-generate incremental release logs, and push assets directly to cloud distribution endpoints.

---

## 💻 Getting Started

### Installation
The application uses a fire-and-forget installer with silent, automated background updates managed entirely through cloud release points:
1. Navigate to the latest release on the project's GitHub page.
2. Download and execute the `Setup.exe` file.
3. Velopack will handle the local workstation deployment and future delta updates seamlessly.

### Building from Source
To compile the environment locally, you require:
* **Visual Studio 2022** (with `.NET desktop development` workload enabled)
* **.NET 9 SDK**

```bash
# Clone the repository
git clone <repository-url>
```
1. Open the solution file (.sln) in Visual Studio.

2. Restore the required NuGet packages.

3. Build and launch the AMFormsCST.Desktop startup project.
