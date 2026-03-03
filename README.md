# ZaffreMeld ERP — ASP.NET Core Edition

Converted from the original **BlueSeer Java ERP** (v7.0, MIT License) to **ASP.NET Core 10 / C#**.

Original Project: https://github.com/blueseerERP/blueseer

ZaffreMeld ERP

ZaffreMeld is a comprehensive, open-source Enterprise Resource Planning suite offered entirely for free. Tailored specifically for the manufacturing sector, it provides a versatile foundation that is both simple to adapt and easy to scale. While it comes equipped with the standard features essential for industrial operations, its architecture allows for deep customization to fit unique business models.

Beyond core ERP functions, ZaffreMeld features a robust, integrated EDI engine. This tool streamlines electronic data translations and provides end-to-end monitoring for all inbound and outbound file traffic.

Distributed under the MIT License, ZaffreMeld is open for unrestricted use. The full source code and installation packages are hosted on GitHub.
Core Capabilities

ZaffreMeld organizes business logic into modular components covering a wide array of operational needs:

* Financial Management: Double-entry general ledger, cost accounting, and comprehensive A/R and A/P tracking with aging reports.

* Operations & Production: Inventory control, multi-level job tracking, shop floor scanning, and full lot traceability.

* Supply Chain: Streamlined purchasing, sales order management, and Freight/Logistics coordination.

* Service & Billing: Support for recurring service contracts, quoting, and dedicated service order workflows.

* Connectivity: System-to-system APIs and an automated task scheduler (Cron) for background processes.

* Advanced EDI Suite: * Translation Support: X12, EDIFACT, CSV, FlatFiles (IDOC), XML, and JSON.

* Communication Protocols: Native FTP and AS2 (both server and client side).

* Compliance & Planning: UCC label generation, Materials Resource Planning (MRP), and Human Resources (HR) management.

* Workforce: Integrated Payroll processing.


**Currently In Alpha Build. Not recommended for any production workflow.**
---

## Architecture Overview

### Original Java → .NET Core Mapping

| Java (Original) | ASP.NET Core (Converted) |
|---|---|
| `HttpServlet` (doGet/doPost) | `ControllerBase` API Controllers |
| `javax.servlet.http.Cookie` | `IHttpContextAccessor` / Cookie middleware |
| `javax.servlet.http.HttpSession` | ASP.NET Core Session + JWT |
| JDBC `DriverManager.getConnection()` | Entity Framework Core DbContext |
| `PreparedStatement` / `ResultSet` | LINQ / EF Core queries |
| Java `record` types | C# `record` / POCO classes |
| JSON via Jackson `ObjectMapper` | `System.Text.Json` / Newtonsoft.Json |
| Maven `pom.xml` | `.csproj` |
| `bsmf.MainFrame` static state | `IZaffreMeldAppService` (DI scoped) |
| `BlueSeerUtils.SuccessBit/ErrorBit` | `ServiceResult` record |
| `bslog(ex)` logging | Serilog structured logging |
| Servlet `web.xml` config | `appsettings.json` + `Program.cs` |
| Java Swing desktop UI | Razor MVC Views + Bootstrap 5 |
| `dataServFIN`, `dataServINV`, etc. | `FinanceController`, `InventoryController`, etc. |

---

## Project Structure

```
ZaffreMeld.Web/
├── Controllers/
│   ├── HomeController.cs            # Dashboard + login (web MVC)
│   └── Api/
│       ├── AuthController.cs        # ← authServ.java
│       ├── FinanceController.cs     # ← dataServFIN.java
│       ├── InventoryController.cs   # ← dataServINV.java
│       ├── OrdersController.cs      # ← dataServORD.java + dataServCUS.java
│       ├── PurchShipVendController.cs  # ← dataServPUR + dataServSHP + dataServVDR
│       └── AdminHrProdController.cs # ← dataServADM + dataServHRM + dataServPRD + dataServRCV
├── Data/
│   ├── ZaffreMeldDbContext.cs         # EF Core DbContext (all tables)
│   └── ZaffreMeldDbInitializer.cs     # Seed data (roles, admin user, defaults)
├── Models/
│   ├── Administration/AdminModels.cs
│   ├── Finance/FinanceModels.cs
│   ├── Inventory/InventoryModels.cs
│   ├── Orders/OrderModels.cs
│   └── AllOtherModels.cs            # Purchasing, Shipping, Vendor, HR, EDI, Engineering, Freight, Production, Receiving, Scheduling
├── Services/
│   └── ZaffreMeldServices.cs          # IFinanceService, IInventoryService, IOrderService, IAuthService, IZaffreMeldAppService
├── Middleware/
│   └── RequestLoggingMiddleware.cs  # Mirrors Java bslog() request logging
├── Extensions/
│   └── ServiceExtensions.cs         # DI registration
├── Views/
│   ├── Shared/
│   │   ├── _Layout.cshtml           # Main layout with sidebar nav
│   │   └── _SidebarNav.cshtml       # Navigation menu
│   ├── Home/Index.cshtml            # ERP dashboard
│   └── Account/Login.cshtml         # Login page
├── wwwroot/css/site.css
├── appsettings.json
└── Program.cs                       # App startup, DI, pipeline
```

---

## Module Mapping (Java packages → C# namespaces)

| Java Package | C# Namespace / Module |
|---|---|
| `com.blueseer.fgl` | Finance / General Ledger (`Models.Finance`) |
| `com.blueseer.fap` | Accounts Payable (in `Models.Finance`) |
| `com.blueseer.far` | Accounts Receivable (in `Models.Finance`) |
| `com.blueseer.inv` | Inventory (`Models.Inventory`) |
| `com.blueseer.ctr`, `com.blueseer.ord` | Orders / Customer (`Models.Orders`) |
| `com.blueseer.pur` | Purchasing (`Models.Purchasing`) |
| `com.blueseer.shp` | Shipping (`Models.Shipping`) |
| `com.blueseer.vdr` | Vendor (`Models.Vendor`) |
| `com.blueseer.rcv` | Receiving (`Models.Receiving`) |
| `com.blueseer.hrm`, `com.blueseer.tca` | HR / Time & Attendance (`Models.HR`) |
| `com.blueseer.edi` | EDI (`Models.EDI`) |
| `com.blueseer.eng` | Engineering / ECN (`Models.Engineering`) |
| `com.blueseer.frt` | Freight (`Models.Freight`) |
| `com.blueseer.prd`, `com.blueseer.sch` | Production / Scheduling (`Models.Production`, `Models.Scheduling`) |
| `com.blueseer.dst` | Distribution Orders (in `Models.Distribution`) |
| `com.blueseer.adm`, `com.blueseer.utl` | Administration / Utilities (`Models.Administration`) |

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- One of: **SQLite** (default, zero config), **MySQL 8+**, or **SQL Server 2019+**

---

## Getting Started

### 1. Clone / Extract

```bash
cd ZaffreMeld.Web
```

### 2. Configure Database

Edit `appsettings.json`:

**SQLite (default — no setup needed):**
```json
"ZaffreMeld": { "DatabaseType": "sqlite" },
"ConnectionStrings": { "SqliteConnection": "Data Source=bsdb.db" }
```

**MySQL:**
```json
"ZaffreMeld": { "DatabaseType": "mysql" },
"ConnectionStrings": { "DefaultConnection": "Server=localhost;Database=bsdb;User=bsuser;Password=yourpass;" }
```

**SQL Server:**
```json
"ZaffreMeld": { "DatabaseType": "sqlserver" },
"ConnectionStrings": { "DefaultConnection": "Server=localhost;Database=bsdb;Trusted_Connection=True;" }
```

### 3. Set JWT Secret

```json
"Jwt": { "Key": "YOUR-STRONG-SECRET-KEY-AT-LEAST-32-CHARACTERS-LONG" }
```

Or use user secrets (recommended):
```bash
dotnet user-secrets set "Jwt:Key" "your-secret-key-here"
```

### 4. Run

```bash
dotnet run
```

Navigate to `https://localhost:5001`

**Default credentials:** `admin` / `Admin1234!`

---

## Database Migration

The app uses EF Core code-first migrations. The database is created automatically on first run via `EnsureCreated()`.

For production migrations:
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

## API Reference

Full interactive API documentation is available at:
```
https://localhost:5001/api-docs
```

### Key Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/auth/login` | Authenticate, get JWT |
| `POST` | `/api/auth/logout` | End session |
| `GET` | `/api/finance/accounts` | Chart of accounts |
| `POST` | `/api/finance/glpair` | Post balanced GL entry |
| `GET` | `/api/inventory/items` | Search items |
| `POST` | `/api/orders/sales` | Create sales order |
| `GET` | `/api/orders/customers` | Search customers |
| `POST` | `/api/purchasing/orders` | Create purchase order |
| `POST` | `/api/shipping/shippers` | Create shipper |
| `POST` | `/api/receiving` | Create receiver |
| `GET` | `/api/finance/ar/aging` | AR aging report |
| `GET` | `/api/finance/ap/aging` | AP aging report |

All endpoints except `/api/auth/login` require `Authorization: Bearer {token}` header.

---

## Key Conversion Notes

### ServiceResult Pattern
The Java code returned `String[] { "0", "message" }` (0=success, 1=error). This is replaced by:
```csharp
public record ServiceResult(bool Success, string Message, object? Data = null);
```

### Document Numbering
Java used a counter table with synchronized methods. C# uses `SemaphoreSlim` + EF Core transactions in `BlueSeerAppService.GetNextDocumentNumber()`.

### Authentication
Java used session cookies + a `HashMap<String, String>` in `authServ.java`. C# uses ASP.NET Core Identity + JWT Bearer tokens, with the token stored in an HttpOnly cookie for MVC views and in the Authorization header for API calls.

### Multi-Database Support
The original Java supported MySQL, SQLite, and remote DB via SSH tunnel. This version supports MySQL, SQLite, and SQL Server via EF Core providers, configured in `appsettings.json`.

### Logging
`bsmf.MainFrame.bslog(ex)` → `ILogger<T>` injected via DI, backed by Serilog with console + rolling file output.

---

## License

MIT License — same as original BlueSeer ERP.
Copyright (c) Terry Evans Vaughn (original Java)
ASP.NET Core conversion — same MIT terms apply.
