using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using FEENALOoFINALE.Models.DTOs; // Add this line
using FEENALOoFINALE.Models;

namespace FEENALOoFINALE.Hubs
{
    [Authorize]
    public class MaintenanceHub : Hub
    {
        /// <summary>
        /// Join a specific group for targeted notifications
        /// </summary>
        /// <param name="groupName">Group name (e.g., "Dashboard", "Alerts", "Maintenance")</param>
        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            
            // Notify other clients in the group that someone joined
            await Clients.Group(groupName).SendAsync("UserJoined", Context.User?.Identity?.Name);
        }

        /// <summary>
        /// Leave a specific group
        /// </summary>
        /// <param name="groupName">Group name to leave</param>
        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            await Clients.Group(groupName).SendAsync("UserLeft", Context.User?.Identity?.Name);
        }

        /// <summary>
        /// Send a message to all connected clients
        /// </summary>
        /// <param name="message">Message to broadcast</param>
        public async Task SendMessageToAll(string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", Context.User?.Identity?.Name, message);
        }

        /// <summary>
        /// Send a notification to a specific group
        /// </summary>
        /// <param name="groupName">Target group</param>
        /// <param name="type">Notification type (success, warning, error, info)</param>
        /// <param name="title">Notification title</param>
        /// <param name="message">Notification message</param>
        public async Task SendNotificationToGroup(string groupName, string type, string title, string message)
        {
            if (string.IsNullOrWhiteSpace(groupName) || string.IsNullOrWhiteSpace(message))
                return;

            await Clients.Group(groupName).SendAsync("ReceiveNotification", new
            {
                Type = type ?? "info",
                Title = title ?? "Notification",
                Message = message,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Send maintenance update to all clients
        /// </summary>
        /// <param name="updateType">Type of update (created, updated, completed)</param>
        /// <param name="maintenanceId">Maintenance task ID</param>
        /// <param name="message">Update message</param>
        public async Task SendMaintenanceUpdate(string updateType, int maintenanceId, string message)
        {
            if (string.IsNullOrWhiteSpace(updateType) || maintenanceId <= 0)
                return;

            await Clients.All.SendAsync("MaintenanceUpdate", new
            {
                UpdateType = updateType,
                MaintenanceId = maintenanceId,
                Message = message ?? $"Maintenance task {updateType}",
                Timestamp = DateTime.UtcNow,
                UserId = Context.User?.Identity?.Name
            });
        }

        /// <summary>
        /// Handle connection events
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.Identity?.Name;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Dashboard");
                await Clients.All.SendAsync("UserConnected", userId);
            }
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Handle disconnection events
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.Identity?.Name;
            if (!string.IsNullOrEmpty(userId))
            {
                await Clients.All.SendAsync("UserDisconnected", userId);
            }
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Send equipment status update
        /// </summary>
        public async Task SendEquipmentStatusUpdate(int equipmentId, string status, string message)
        {
            if (equipmentId <= 0 || string.IsNullOrWhiteSpace(status))
                return;

            await Clients.Group("Dashboard").SendAsync("EquipmentStatusUpdate", new
            {
                EquipmentId = equipmentId,
                Status = status,
                Message = message ?? $"Equipment status changed to {status}",
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Send alert notification using DTO to avoid circular references
        /// </summary>
        /// <param name="alert">Alert to send</param>
        public async Task SendAlertNotification(Alert alert)
        {
            if (alert == null) return;
            
            var alertDto = AlertNotificationDto.FromAlert(alert);
            await Clients.Group("Dashboard").SendAsync("AlertCreated", alertDto);
            await Clients.Group("Alerts").SendAsync("AlertCreated", alertDto);
        }
        
        /// <summary>
        /// Send alert update notification using DTO
        /// </summary>
        /// <param name="alert">Updated alert</param>
        public async Task SendAlertUpdate(Alert alert)
        {
            if (alert == null) return;
            
            var alertDto = AlertNotificationDto.FromAlert(alert);
            await Clients.Group("Dashboard").SendAsync("AlertUpdated", alertDto);
            await Clients.Group("Alerts").SendAsync("AlertUpdated", alertDto);
        }
    }
}
