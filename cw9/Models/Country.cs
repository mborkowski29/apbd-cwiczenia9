using System;
using System.Collections.Generic;
namespace cw9.Models;

public class Country
{
    public int IdCountry { get; set; }
    public string Name { get; set; } = null!;

    public virtual ICollection<Trip> IdTrips { get; set; } = new List<Trip>();
}