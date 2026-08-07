using System.Threading;
using System.Threading.Tasks;

namespace PhotoBooth.Core.Interfaces;

public interface IPrintService
{
    Task<bool> PrintImageAsync(string imagePath, string printerName, int copies, CancellationToken ct, string mediaType = "");
}
