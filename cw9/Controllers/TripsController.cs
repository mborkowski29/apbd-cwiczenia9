using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using cw9.Data;
using cw9.DTOs;
using cw9.Models;

namespace cw9.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TripsController : ControllerBase
    {
        private readonly MasterContext _masterContext;

        public TripsController(MasterContext masterContext)
        {
            _masterContext = masterContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetTrips(int pageNum = 1, int pageSize = 10)
        {
            var zapOWycieczki = _masterContext.Trips
                .Include(x => x.ClientTrips).ThenInclude(cTrip => cTrip.IdClientNavigation)
                .Include(x => x.IdCountries)
                .OrderBy(x => x.Name)
                .AsQueryable();

            var liczbaRekordow = await zapOWycieczki.CountAsync();

            var wycieczki = await zapOWycieczki
                .Skip((pageNum - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new TripsDTO()
                {
                    Name = x.Name,
                    Description = x.Description,
                    DateFrom = x.DateFrom,
                    DateTo = x.DateTo,
                    MaxPeople = x.MaxPeople,
                    Countries = x.IdCountries.Select(c => new CountryDTO()
                    {
                        Name = c.Name
                    }).ToList(),
                    Clients = x.ClientTrips.Select(cTrip => new ClientsDTO()
                    {
                        FirstName = cTrip.IdClientNavigation.FirstName,
                        LastName = cTrip.IdClientNavigation.LastName
                    }).ToList()
                })
                .ToListAsync();

            return Ok(new
            {
                PageNumber = pageNum,
                PageSize = pageSize,
                TotalRecords = liczbaRekordow,
                Trips = wycieczki
            });
        }

        [HttpDelete("{idClient}")]
        public async Task<IActionResult> DeleteClient(int idClient)
        {
            var klient = await _masterContext.Clients.FindAsync(idClient);
            if (klient == null)
                return NotFound("nie znaleziono klienta");
            
            var czyMaWycieczki = await _masterContext.ClientTrips.AnyAsync(cTrip => cTrip.IdClient == idClient);
            if (czyMaWycieczki)
                return BadRequest("klient jest przypisany i nie mozna go usunac");
            
            _masterContext.Clients.Remove(klient);
            await _masterContext.SaveChangesAsync();

            return Ok("klient zostal usuniety");
        }

        [HttpPost("{idTrip}/clients")]
        public async Task<IActionResult> RegisterClientToTrip(int idTrip, [FromBody] ClientTripDTO clientToTripDto)
        {
            var istKlient = await _masterContext.Clients.FirstOrDefaultAsync(x => x.Pesel == clientToTripDto.Pesel);

            if (istKlient != null)
                return Conflict("klient juz istnieje");
            
            var nowyKlient = new Client
            {
                FirstName = clientToTripDto.FirstName,
                LastName = clientToTripDto.LastName,
                Email = clientToTripDto.Email,
                Telephone = clientToTripDto.Telephone,
                Pesel = clientToTripDto.Pesel
            };
            _masterContext.Clients.Add(nowyKlient);
            await _masterContext.SaveChangesAsync();

            var wycieczka = await _masterContext.Trips.FirstOrDefaultAsync(t => t.IdTrip == idTrip);
            if (wycieczka == null || wycieczka.DateFrom <= DateTime.Now)
                return BadRequest("tej wycieczki nie ma albo juz sie zaczela");
            

            var isRegistered = await _masterContext.ClientTrips.AnyAsync(cTrip => cTrip.IdClient == nowyKlient.IdClient && cTrip.IdTrip == wycieczka.IdTrip);
            if (isRegistered)
                return Conflict("ten klient jest juz zarejestrowany na tą wycieczke");
            

            var clientTrip = new ClientTrip
            {
                IdClient = nowyKlient.IdClient,
                IdTrip = wycieczka.IdTrip,
                RegisteredAt = DateTime.UtcNow,
                PaymentDate = clientToTripDto.PaymentDate
            };
            _masterContext.ClientTrips.Add(clientTrip);
            await _masterContext.SaveChangesAsync();

            return Ok("klient zostal przypisany");
        }
    }
}
