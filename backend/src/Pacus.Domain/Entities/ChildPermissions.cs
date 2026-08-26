namespace Pacus.Domain.Entities;

public class ChildPermissions
{
    public bool CanCreateTasks { get; set; } = true;
    public bool CanEditTasks { get; set; } = true;
    public bool CanDeleteTasks { get; set; } = true;
    public bool CanReorderTasks { get; set; } = true;
    public bool CanSetPoints { get; set; } = true;
}
