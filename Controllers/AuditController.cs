using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRManagementSystem.Data;
using HRManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using X.PagedList;
using X.PagedList.Extensions;
using ClosedXML.Excel;
using System.Text;

namespace HRManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AuditController : Controller
    {
        private readonly AppDbContext _context;

        public AuditController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? searchUser, string? searchTable, int? page)
        {
            const int pageSize = 20;
            int pageNumber = page ?? 1;

            var query = _context.AuditLogs
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchUser))
                query = query.Where(a => a.UserName.Contains(searchUser));

            if (!string.IsNullOrWhiteSpace(searchTable))
                query = query.Where(a => a.TableName.Contains(searchTable));

            query = query.OrderByDescending(a => a.Timestamp);

            ViewBag.SearchUser = searchUser;
            ViewBag.SearchTable = searchTable;

            var auditLogs = await query.ToListAsync();
            return View(auditLogs.ToPagedList(pageNumber, pageSize));
        }

        public async Task<IActionResult> Details(int id)
        {
            var auditLog = await _context.AuditLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AuditLogId == id);

            if (auditLog == null)
                return NotFound();

            return View(auditLog);
        }

        // Export filtered audit logs to CSV
        [HttpGet]
        public async Task<IActionResult> ExportCsv(string? searchUser, string? searchTable)
        {
            var query = _context.AuditLogs
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchUser))
                query = query.Where(a => a.UserName.Contains(searchUser));

            if (!string.IsNullOrWhiteSpace(searchTable))
                query = query.Where(a => a.TableName.Contains(searchTable));

            var logs = await query.OrderByDescending(a => a.Timestamp).ToListAsync();

            var sb = new StringBuilder();
            // Header
            sb.AppendLine("AuditLogId,Timestamp,UserId,UserName,TableName,Action,RecordId,Changes");

            foreach (var a in logs)
            {
                // CSV-safe values (escape quotes and wrap with quotes if needed)
                string Escape(string? v)
                {
                    if (string.IsNullOrEmpty(v)) return string.Empty;
                    var needsQuotes = v.Contains(',') || v.Contains('"') || v.Contains('\n') || v.Contains('\r');
                    v = v.Replace("\"", "\"\"");
                    return needsQuotes ? $"\"{v}\"" : v;
                }

                sb.Append(a.AuditLogId).Append(',')
                  .Append('"').Append(a.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")).Append('"').Append(',')
                  .Append(Escape(a.UserId)).Append(',')
                  .Append(Escape(a.UserName)).Append(',')
                  .Append(Escape(a.TableName)).Append(',')
                  .Append(Escape(a.Action)).Append(',')
                  .Append(a.RecordId?.ToString() ?? string.Empty).Append(',')
                  .Append(Escape(a.Changes))
                  .AppendLine();
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"AuditLogs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }

        // Export filtered audit logs to Excel
        [HttpGet]
        public async Task<IActionResult> ExportExcel(string? searchUser, string? searchTable)
        {
            var query = _context.AuditLogs
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchUser))
                query = query.Where(a => a.UserName.Contains(searchUser));

            if (!string.IsNullOrWhiteSpace(searchTable))
                query = query.Where(a => a.TableName.Contains(searchTable));

            var logs = await query.OrderByDescending(a => a.Timestamp).ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Audit Logs");
            ws.Cell(1, 1).Value = "Audit Log ID";
            ws.Cell(1, 2).Value = "Timestamp";
            ws.Cell(1, 3).Value = "User ID";
            ws.Cell(1, 4).Value = "User Name";
            ws.Cell(1, 5).Value = "Table";
            ws.Cell(1, 6).Value = "Action";
            ws.Cell(1, 7).Value = "Record ID";
            ws.Cell(1, 8).Value = "Changes";

            for (int i = 0; i < logs.Count; i++)
            {
                var a = logs[i];
                int r = i + 2;
                ws.Cell(r, 1).Value = a.AuditLogId;
                ws.Cell(r, 2).Value = a.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                ws.Cell(r, 3).Value = a.UserId;
                ws.Cell(r, 4).Value = a.UserName;
                ws.Cell(r, 5).Value = a.TableName;
                ws.Cell(r, 6).Value = a.Action;
                ws.Cell(r, 7).Value = a.RecordId;
                ws.Cell(r, 8).Value = a.Changes;
            }

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            var fileName = $"AuditLogs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
    }
}