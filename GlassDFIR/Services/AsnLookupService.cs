using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace GlassDFIR.Services
{
    public class AsnLookupService : IAsnLookupService
    {
        private readonly IDownloaderService _downloaderService;
        private readonly string _asnDataPath;
        private List<AsnRange> _ranges = new List<AsnRange>();
        
        public bool IsReady { get; private set; }

        public AsnLookupService(IDownloaderService downloaderService)
        {
            _downloaderService = downloaderService;
            _asnDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "AsnData", "kusto-cidr-asn.csv");
        }

        public async Task InitializeAsync()
        {
            if (!File.Exists(_asnDataPath))
            {
                await _downloaderService.DownloadAsnDataAsync();
            }

            if (File.Exists(_asnDataPath))
            {
                await Task.Run(() => LoadData());
                IsReady = _ranges.Count > 0;
            }
        }

        private void LoadData()
        {
            var ranges = new List<AsnRange>();
            try
            {
                using (var reader = new StreamReader(_asnDataPath))
                {
                    // Skip header
                    reader.ReadLine();

                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var parts = line.Split(',');
                        if (parts.Length >= 3)
                        {
                            var cidr = parts[0].Trim('"');
                            var asn = parts[1].Trim('"');
                            var name = parts[2].Trim('"');

                            var range = ParseCidr(cidr);
                            if (range != null)
                            {
                                range.AsnNumber = asn;
                                range.AsnName = name;
                                ranges.Add(range);
                            }
                        }
                    }
                }

                _ranges = ranges.OrderBy(r => r.Start).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading ASN data: {ex.Message}");
            }
        }

        public (string ASN, string Name)? GetAsnInfo(string ip)
        {
            if (!IsReady || string.IsNullOrEmpty(ip)) return null;

            if (!IPAddress.TryParse(ip, out var address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                return null;

            uint ipUint = IpToUint(address);

            // Binary search
            int low = 0;
            int high = _ranges.Count - 1;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                var range = _ranges[mid];

                if (ipUint >= range.Start && ipUint <= range.End)
                {
                    return (range.AsnNumber, range.AsnName);
                }

                if (ipUint < range.Start)
                {
                    high = mid - 1;
                }
                else
                {
                    low = mid + 1;
                }
            }

            return null;
        }

        private AsnRange? ParseCidr(string cidr)
        {
            var parts = cidr.Split('/');
            if (parts.Length != 2) return null;

            if (!IPAddress.TryParse(parts[0], out var ip)) return null;
            if (!int.TryParse(parts[1], out int prefix)) return null;

            uint ipUint = IpToUint(ip);
            uint mask = prefix == 0 ? 0 : uint.MaxValue << (32 - prefix);
            
            return new AsnRange
            {
                Start = ipUint & mask,
                End = ipUint | ~mask
            };
        }

        private uint IpToUint(IPAddress ip)
        {
            byte[] bytes = ip.GetAddressBytes();
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        private class AsnRange
        {
            public uint Start { get; set; }
            public uint End { get; set; }
            public string AsnNumber { get; set; } = string.Empty;
            public string AsnName { get; set; } = string.Empty;
        }
    }
}
