using AutoMapper;
using TM.Contracts.Auth;
using TM.Model.Entities;

namespace TM.ServiceLogic.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<User, AuthResponse>()
                .ForMember(dest => dest.Token, opt => opt.Ignore());
            
        }
    }
}