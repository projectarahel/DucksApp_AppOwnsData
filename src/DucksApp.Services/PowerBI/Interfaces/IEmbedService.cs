﻿using DucksApp.Services.Models;
using System.Threading.Tasks;

namespace DucksApp.Services.PowerBI
{
    public interface IEmbedService
    {
        Task SetReportEmbedConfigAsync();
        EmbedConfig EmbedConfig { get; }
        Task<bool> RefreshDatasetAsync();
    }
}