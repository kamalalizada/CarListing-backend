using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entry.Dto;
public class UploadImagesResultDto
{
    public int CarId { get; set; }
    public List<CarImageDto> Images { get; set; } = new();
}
