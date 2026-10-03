using System;

namespace ContentOS.Domain;

public interface IHasId
{
	Guid Id { get; set; }
}
