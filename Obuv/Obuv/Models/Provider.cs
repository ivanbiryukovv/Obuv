using System;
using System.Collections.Generic;

namespace Obuv.Models;

public partial class Provider
{
    public int ProviderId { get; set; }

    public string? Provider1 { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
