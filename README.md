Task Management Web API:
A robust backend system built with ASP.NET Core and Entity Framework Core for managing user tasks. This API features a hierarchical role-based system (Admin/User), JWT authentication, and advanced task assignment logic.

Features:
Secure Authentication: JWT-based login and registration system using BCrypt for password hashing.

Role-Based Access Control (RBAC): Distinct permissions for Admin and User roles.

Task Management: Full CRUD operations for tasks including title, description, and status tracking.

Smart Soft Delete:

Preserves data integrity by marking users and tasks as IsDeleted instead of permanent removal.

Safety Guards: Prevents deleting users who have active InProgress tasks.

Admin Protection: Built-in security check to prevent Admins from deleting other Admin accounts.

Unassigned Task Support: Uses 0 as a default unassigned state, mapping to NULL in the database for better logic handling.

Tech Stack:
Framework: .NET 8 / ASP.NET Core Web API

Database: Microsoft SQL Server (MSSQL)

ORM: Entity Framework Core

Mapping: AutoMapper

Security: JWT (JSON Web Tokens) & BCrypt.Net
