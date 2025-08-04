

while (true) {
    try
    {
        var c = new HttpClient();
        var stream = await c.GetStreamAsync("http://localhost:10401/service/tenant-api/logs");
        // using var stream = await client.ReadNamespacedPodLogAsync(pod.Name(), pod.Namespace(), follow: true);
        using var sr = new StreamReader(stream);
        while (true)
        {
            var line = await sr.ReadLineAsync();
            Console.WriteLine(line);
        }
    }
    catch
    {
        Console.Error.WriteLine("Reconnecting");
        await Task.Delay(1000);
    }
}