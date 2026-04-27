using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Tutorial7.DTOs;
using Tutorial7.Services;

namespace Tutorial7.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentsController : ControllerBase
    {
        private readonly string _connectionString;

        public AppointmentsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                                ?? throw new ArgumentException("Missing connection string");
        }

        [HttpGet]
        public async Task<IActionResult> GetApppointmenrs([FromQuery] string? status,
            [FromQuery] string? patientLastName)
        {


            var appoitments = new List<AppointmentListDto>();
            await using var connection = new SqlConnection(_connectionString);
            var query = """
                        SELECT 
                            a.IdAppointment, a.AppointmentDate, a.Status, a.Reason,
                            p.FirstName + ' ' + p.LastName AS PatientFullName, p.Email AS PatientEmail
                        FROM dbo.Appointments a
                        JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                        WHERE (@Status IS NULL OR a.Status = @Status)
                          AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
                        ORDER BY a.AppointmentDate;
                        """;
            await using var command = new SqlCommand(query, connection);

            command.Parameters.AddWithValue("@Status", string.IsNullOrEmpty(status) ? DBNull.Value : status);
            command.Parameters.AddWithValue("@PatientLastName", string.IsNullOrEmpty(patientLastName) ? DBNull.Value : patientLastName);

            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                appoitments.Add(new AppointmentListDto
                {
                    IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
                    AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
                    Status = reader.GetString(reader.GetOrdinal("Status")),
                    Reason = reader.GetString(reader.GetOrdinal("Reason")),
                    PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
                    PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail")),

                });
            }

            return Ok(appoitments);

        }
    }
}


