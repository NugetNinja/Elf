﻿namespace Elf.Api.Models;

public class RequestTrack
{
    public string FwToken { get; set; }

    public string Note { get; set; }

    public string UserAgent { get; set; }

    public string IpAddress { get; set; }

    public DateTime RequestTimeUtc { get; set; }

    public string IPCountry { get; set; }

    public string IPRegion { get; set; }

    public string IPCity { get; set; }

    public string IPASN { get; set; }

    public string IPOrg { get; set; }
}
