# HC Management System (Razor Pages, .NET 8)

A clean, independent HR Management System built with ASP.NET Core 8 Razor Pages. This repository contains only the HR system (no ESS Leave System). It manages employees, departments, organization charts, analytics, exports, and more.

Live Demo
- Production (Render): https://hr-management-system-jstj.onrender.com/
- Hangfire Dashboard: https://hr-management-system-jstj.onrender.com/hangfire

Demo Credentials
- Email: admin@hrsystem.com
- Password: Admin@123

Key Features
- Employees: CRUD, profile photo upload, salary, department, manager, employment type, soft delete (recycle bin)
- Status: Editable dropdown (Active, OnLeave, Inactive) feeding homepage and dashboard stats
- Departments: management and analytics
- Line managers: self-referencing relationships and Org Chart
- Dates: birthdays and work anniversaries (next 30 days)
- Dashboard: recent hires, birthdays, anniversaries, status breakdown, dept distribution
- Exports: Excel, CSV, and PDF (QuestPDF)
- Background jobs (Hangfire): birthdays, anniversaries, reports
- Localisation: en-ZA (default) and en-US via /set-culture endpoint
- API: Employees API (+ Swagger in Development)

Tech Stack
- .NET 8, Razor Pages
- EF Core (SQLite) + ASP.NET Core Identity
- Hangfire (InMemory for demo)
- QuestPDF, ClosedXML, X.PagedList
- Bootstrap 5 + Font Awesome

Screenshots
The repository includes screenshots under `docs/screenshots/`.
- Home (Public & Authenticated)
- Employees list and profile
- Dashboard analytics
- Departments & Org Chart
- Admin view & Audit log

Getting Started (Local)
Prerequisites: .NET 8 SDK

Project Structure (selected)
- `Models/` — core domain models (Employee, Department, StatusChangeRequest)
- `Data/` — EF Core contexts and seeding (`AppDbContext`, `DemoDataSeeder`)
- `Services/` — background jobs and utilities
- `Controllers/` — MVC controllers for pages and APIs
- `Views/` — Razor views (Razor Pages/MVC)
- `wwwroot/` — static assets

Status Tracking (Important)
- The `Status` field is an editable dropdown on the Employee edit form.
- Options: `Active`, `OnLeave`, `Inactive`.
- Homepage and Dashboard statistics read directly from this field.
- Soft-deleted employees (`IsDeleted = true`) are excluded from all stats.

Localisation
- Toggle via the Language menu (top-right).
- Endpoint: `/set-culture?culture=en-ZA|en-US&returnUrl={path}`.
- Culture is stored in the `.AspNetCore.Culture` cookie for 1 year.

Render Deployment
This repo ships with a root-level Dockerfile and a single-service `render.yaml` for the HR app.



