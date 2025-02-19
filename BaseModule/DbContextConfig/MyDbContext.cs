﻿
using BaseModule.ActivityManagement.Entity;
using BaseModule.AuditManagement;
using BaseModule.Mapping.ActivityLogMapping;
using BaseModule.Mapping.AuditMapping;
using BaseModule.Mapping.EmailModuleMapping;
using BaseModule.Mapping.User;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using UserModule.Entity;

namespace BaseModule.DbContextConfig
{
    public  class MyDbContext : IdentityDbContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        
        public MyDbContext(DbContextOptions<MyDbContext> options, IHttpContextAccessor httpContextAccessor) : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

      

        #region Audit and activity log
        public DbSet<Audit> AuditLogs { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        #endregion
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
          
            #region userMapping
            modelBuilder.ApplyConfiguration(new UserEntityMapping());
            #endregion

            #region audit and activityLogMapping
            modelBuilder.ApplyConfiguration(new AuditEntityMapping());
            modelBuilder.ApplyConfiguration(new ActivityLogMappingConfig());
            #endregion
            modelBuilder.ApplyConfiguration(new TemplateMapping());
        }

        public virtual async Task<int> SaveChangesAsync(bool isTracked = true)
        {
            if(isTracked)
            {
                OnBeforeSaveChanges();
            }          
            var result = await base.SaveChangesAsync();
            return result;
        }
        private void OnBeforeSaveChanges()
        {
            var userId = "";
            if(_httpContextAccessor.HttpContext.User.Claims.Count() >0)
            {
                 userId = _httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;

            }
            var ipAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString();
              var browser = _httpContextAccessor.HttpContext.Request.Headers["user-agent"].ToString();
            ChangeTracker.DetectChanges();
            var auditEntries = new List<AuditEntry>();
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is Audit || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;
                var auditEntry = new AuditEntry(entry);
                auditEntry.TableName = entry.Entity.GetType().Name;
                auditEntry.UserId = userId;
                auditEntry.IpAddress = ipAddress;
                auditEntry.Browser = browser;
                auditEntries.Add(auditEntry);
                foreach (var property in entry.Properties)
                {
                    string propertyName = property.Metadata.Name;
                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[propertyName] = property.CurrentValue;
                        continue;
                    }
                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.AuditType = AuditType.Create;
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                            break;
                        case EntityState.Deleted:
                            auditEntry.AuditType = AuditType.Delete;
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            break;
                        case EntityState.Modified:
                            if (property.IsModified)
                            {
                                auditEntry.ChangedColumns.Add(propertyName);
                                auditEntry.AuditType = AuditType.Update;
                                auditEntry.OldValues[propertyName] = property.OriginalValue;
                                auditEntry.NewValues[propertyName] = property.CurrentValue;
                            }
                            break;
                    }
                }
            }
            foreach (var auditEntry in auditEntries)
            {
                AuditLogs.Add(auditEntry.ToAudit());
            }
        }



    }
}
