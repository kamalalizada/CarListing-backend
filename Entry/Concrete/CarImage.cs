using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entry.Concrete
{
    public class CarImage : Base
    {
        public string ImageUrl { get; set; }
        public string? ObjectKey { get; set; }
        public bool IsMain { get; set; }
        public int Order { get; set; }

        public int CarId { get; set; }
        public Car Car { get; set; }
    }
}

