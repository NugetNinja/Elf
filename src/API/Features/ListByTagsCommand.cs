﻿using Elf.Api.Data;
using Elf.Shared;
using Microsoft.EntityFrameworkCore;

namespace Elf.Api.Features;

public record ListByTagsCommand(ListByTagsRequest Payload) : IRequest<(List<LinkModel> Links, int TotalRows)>;

public class ListByTagsCommandHandler(ElfDbContext dbContext) : IRequestHandler<ListByTagsCommand, (List<LinkModel> Links, int TotalRows)>
{
    public async Task<(List<LinkModel> Links, int TotalRows)> Handle(ListByTagsCommand request, CancellationToken ct)
    {
        var query = from l in dbContext.Link.Include(l => l.Tags)
                    where l.Tags.Any(t => request.Payload.TagIds.Contains(t.Id))
                    select l;

        var totalRows = query.Count();
        var data = await query.OrderByDescending(p => p.UpdateTimeUtc)
            .Skip(request.Payload.Offset)
            .Take(request.Payload.Take)
            .AsNoTracking()
            .Select(p => new LinkModel
            {
                Id = p.Id,
                OriginUrl = p.OriginUrl,
                Note = p.Note,
                TTL = p.TTL,
                UpdateTimeUtc = p.UpdateTimeUtc,
                AkaName = p.AkaName,
                FwToken = p.FwToken,
                IsEnabled = p.IsEnabled,
                Tags = p.Tags.ToArray()
            })
            .ToListAsync(ct);

        return (data, totalRows);
    }
}