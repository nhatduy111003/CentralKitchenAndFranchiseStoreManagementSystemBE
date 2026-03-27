namespace CentralKitchenAndFranchise.DTO.Responses.Common;

public class FileExportPayload
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = "export.bin";
}