namespace Tutorial7.DTOs;

public class AppointmentDetailsDto : AppointmentListDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string DoctorFullName { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public string? InternalNotes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}