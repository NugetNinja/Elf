﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json.Serialization;

namespace Elf.Api.Data;

public class TagEntity
{
    public TagEntity()
    {
        Links = new HashSet<LinkEntity>();
    }

    public int Id { get; set; }

    public string Name { get; set; }

    [JsonIgnore]
    public virtual ICollection<LinkEntity> Links { get; set; }
}

internal class TagConfiguration : IEntityTypeConfiguration<TagEntity>
{
    public void Configure(EntityTypeBuilder<TagEntity> builder)
    {
        builder.Property(e => e.Name).HasMaxLength(32);
    }
}