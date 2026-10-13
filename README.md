# 3D-Printing-Operations-Management-System
A web application for running a 3D printing shop. It handles the full workflow
from customer order to production, payment and delivery, and keeps product
stock accurate automatically.

## Features
- **Orders:** create, edit and track orders (Draft → Confirmed → Completed / On Hold / Canceled)
- **Business rules:** an order cannot be completed until it is fully paid and delivered
- **Inventory:** stock is the sum of signed inventory transactions
  (sales deduct stock on confirmation; cancelling an order returns it)
- **Cancel with refund:** cancelling restores stock and marks completed payments as REFUNDED
- **Production jobs:** separate status workflow per job, with printer assignment
- **Customers, products and services catalog**
- **Materials and purchase orders**, with stock adjustments
- **Payments and deliveries**
- **Printers:** maintenance records and print profiles
- **Employees, users and login**
- Only in-stock products are offered when creating an order

## Tech stack
- ASP.NET Core 8 (Razor Pages), C#
- Entity Framework Core
- SQL Server (LocalDB for development), with views, triggers and CHECK constraints
- Bootstrap 5

## Getting started
1. Install the .NET 8 SDK and SQL Server LocalDB.
2. Restore the database `Production3DPrintDB` (script or backup in `/database`).
3. Set the connection string in `appsettings.json`:
   `Server=(localdb)\\MSSQLLocalDB;Database=Production3DPrintDB;Trusted_Connection=True;TrustServerCertificate=True`
4. Run:dotnet restore
dotnet run
5. Open the URL shown in the console.

## Project structure
- `Pages/` – Razor Pages (Orders, Customers, Materials, ProductionJobs, …)
- `Models/` – entities and `AppDbContext`
- `Common/` – workflow rules, constants
- `wwwroot/` – static files (CSS, images)

## Status
In active development.<img width="8192" height="5286" alt="Order Management Ecosystem-2026-08-30-095905" src="https://github.com/user-attachments/assets/3d04f7e9-9557-4ba9-a37b-ad75753505c9" />
<img width="959" height="419" alt="image" src="https://github.com/user-attachments/assets/ba1062ed-6534-4c8e-a49a-89e7e71846d2" />

