﻿using Elf.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Elf.Api.Features;

public record GetTokenByAkaNameQuery(string AkaName) : IRequest<string>;

public class GetTokenByAkaNameQueryHandler : IRequestHandler<GetTokenByAkaNameQuery, string>
{
    private readonly ElfDbContext _dbContext;

    public GetTokenByAkaNameQueryHandler(ElfDbContext dbContext) => _dbContext = dbContext;

    public async Task<string> Handle(GetTokenByAkaNameQuery request, CancellationToken cancellationToken)
    {
        var link = await _dbContext.Link.FirstOrDefaultAsync(p => p.AkaName == request.AkaName, cancellationToken);

        return link?.FwToken;
    }
}