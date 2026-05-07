using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entry.Dto;
public class ReorderImagesDto
{
    public List<int> ImageIds { get; set; } = new();
}
