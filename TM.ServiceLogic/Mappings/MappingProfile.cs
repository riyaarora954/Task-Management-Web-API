using AutoMapper;
using TM.Contracts.Auth;
using TM.Contracts.Comments;
using TM.Contracts.Tasks;
using TM.Model.Entities;

namespace TM.ServiceLogic.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<User, AuthResponse>()
                .ForMember(dest => dest.Token, opt => opt.Ignore());

            CreateMap<TM.Model.Entities.Task, TaskResponse>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.AssignedUserName,
                           opt => opt.MapFrom(src => src.AssignedUser != null ? src.AssignedUser.Username : "Unassigned"));
            
            CreateMap<TaskCreateRequest, TM.Model.Entities.Task>();
            CreateMap<Comment, CommentResponse>().ForMember(dest => dest.AuthorName, opt => opt.MapFrom(src => src.User.Username));

        }
    }
}