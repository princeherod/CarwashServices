using CRM.Domain.Entities;
using CRM.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly MasterErpDbContext _masterDb;

    public DevicesController(MasterErpDbContext masterDb)
    {
        _masterDb = masterDb;
    }

    // GET: api/devices
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var devices = await _masterDb.Devices
            .AsNoTracking()
            .OrderBy(d => d.DeviceId)
            .ToListAsync();

        return Ok(devices);
    }

    // GET: api/devices/1
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var device = await _masterDb.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DeviceId == id);

        if (device is null)
        {
            return NotFound(new { message = $"Device {id} not found." });
        }

        return Ok(device);
    }

    // POST: api/devices
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Device device)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var exists = await _masterDb.Devices
            .AnyAsync(d => d.CompanyId == device.CompanyId
                        && d.DeviceCode == device.DeviceCode);

        if (exists)
        {
            return Conflict(new
            {
                message = $"Device '{device.DeviceCode}' already exists for company {device.CompanyId}."
            });
        }

        _masterDb.Devices.Add(device);
        await _masterDb.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = device.DeviceId },
            device);
    }

    // DELETE: api/devices/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var device = await _masterDb.Devices
            .FirstOrDefaultAsync(d => d.DeviceId == id);

        if (device is null)
        {
            return NotFound(new { message = $"Device {id} not found." });
        }

        _masterDb.Devices.Remove(device);
        await _masterDb.SaveChangesAsync();

        return NoContent();
    }
}