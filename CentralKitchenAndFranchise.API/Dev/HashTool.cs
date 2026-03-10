namespace CentralKitchenAndFranchise.API.Dev;

public static class HashTool
{
    public static void Print()
    {
        Console.WriteLine("BCrypt hash for 123456:");
        Console.WriteLine(BCrypt.Net.BCrypt.HashPassword("123456"));
    }
}
