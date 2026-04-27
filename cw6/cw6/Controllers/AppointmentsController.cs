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

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAppointmentAsync([FromRoute] int id)
        {
            await using var connection = new SqlConnection(_connectionString);
            var query = """
                            SELECT 
                            a.IdAppointment, a.AppointmentDate, a.Status, a.Reason, a.InternalNotes, a.CreatedAt,
                            p.FirstName + ' ' + p.LastName AS PatientFullName, p.Email AS PatientEmail, p.PhoneNumber,
                            d.FirstName + ' ' + d.LastName AS DoctorFullName, d.LicenseNumber
                        FROM dbo.Appointments a
                        JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                        JOIN dbo.Doctors d ON d.IdDoctor = a.IdDoctor
                        WHERE a.IdAppointment = @IdAppointment;
                        """;
            
            await using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@IdAppointment", id);
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var d = new AppointmentDetailsDto
                {
                    IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
                    AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
                    Status = reader.GetString(reader.GetOrdinal("Status")),
                    Reason = reader.GetString(reader.GetOrdinal("Reason")),
                    InternalNotes = reader.IsDBNull(reader.GetOrdinal("InternalNotes")) ? null : reader.GetString(reader.GetOrdinal("InternalNotes")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
                    PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail")),
                    PhoneNumber = reader.GetString(reader.GetOrdinal("PhoneNumber")),
                    DoctorFullName = reader.GetString(reader.GetOrdinal("DoctorFullName")),
                    LicenseNumber = reader.GetString(reader.GetOrdinal("LicenseNumber"))
                };
                return Ok(d);
            }
            return NotFound($"Appointment with Id {id} not found");
        }

        [HttpPost]
        public async Task<IActionResult> PostAppointmentAsync([FromBody] CreateAppointmentRequestDto request)
        {
            if (request.AppointmentDate < DateTime.Now)
            {
                return BadRequest("AppointmentDate cannot be in the past");
            }
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var patientCmd = new SqlCommand("SELECT IsActive FROM dbo.Patients WHERE IdPatient = @IdPatient", connection);
            patientCmd.Parameters.AddWithValue("@IdPatient", request.IdPatient);
            var patientActive = await patientCmd.ExecuteScalarAsync() as bool?;
            if (patientActive != true)
            {
                return BadRequest("Patient is not active");
            }

            var doctorCmd = new SqlCommand("SELECT IsActive FROM dbo.Doctors WHERE IdDoctor = @IdDoctor", connection);
            doctorCmd.Parameters.AddWithValue("@IdDoctor", request.IdDoctor);
            var doctorActive = await doctorCmd.ExecuteScalarAsync() as bool?;
            if (doctorActive != true)
            {
                return BadRequest("Doctor is not active");
            }

            var conQuery = """
                           SELECT COUNT(1) FROM dbo.Appointments 
                           WHERE IdDoctor = @IdDoctor AND AppointmentDate = @Date AND Status != 'Cancelled'
                           """;
            var conCmd =  new SqlCommand(conQuery, connection);
            conCmd.Parameters.AddWithValue("@IdDoctor", request.IdDoctor);
            conCmd.Parameters.AddWithValue("@Date", request.AppointmentDate);
            var conCount = (int)(await conCmd.ExecuteScalarAsync() ?? 0);
            if (conCount > 0)
            {
                return Conflict("Doctor already has an appointment at this time");
            }

            var insertQuery = """
                              INSERT INTO dbo.Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason)
                              OUTPUT INSERTED.IdAppointment
                              VALUES (@IdPatient, @IdDoctor, @AppointmentDate, 'Scheduled', @Reason);
                              """;
            await using var insertCmd = new SqlCommand(insertQuery, connection);
            insertCmd.Parameters.AddWithValue("@IdPatient", request.IdPatient);
            insertCmd.Parameters.AddWithValue("@IdDoctor", request.IdDoctor);
            insertCmd.Parameters.AddWithValue("@AppointmentDate", request.AppointmentDate);
            insertCmd.Parameters.AddWithValue("@Reason", request.Reason);
            var newID = (int)(await insertCmd.ExecuteScalarAsync() ?? 0);
            return CreatedAtAction("GetAppointment", new { id = newID }, request);
            
        }
        
    }
    
    
}


