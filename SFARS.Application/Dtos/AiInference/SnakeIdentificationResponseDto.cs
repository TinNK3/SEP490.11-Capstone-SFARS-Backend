using System;
using System.Collections.Generic;
using SFARS.Application.Dtos;

namespace SFARS.Application.Dtos.AiInference
{
    public class SnakeIdentificationResponseDto
    {
        public IdentifiedSnakeDetailDto? PrimarySnake { get; set; }
        public List<SnakeCandidateDto> OtherCandidates { get; set; } = new();
        public string? Note { get; set; }
    }

    public class IdentifiedSnakeDetailDto : SnakeCandidateDto 
    {
        public string? Description { get; set; }
        public string? KeyIdentifiers { get; set; }
        public string? Habitat { get; set; }
        public string? DistributionNote { get; set; }
        public string? Note { get; set; }
    }
}