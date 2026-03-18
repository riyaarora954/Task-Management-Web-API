using AutoMapper;
using TM.Contracts.Auth;
using TM.Contracts.Comments;
using TM.Contracts.Tasks;
using TM.Model.Entities;
using TaskEntity = TM.Model.Entities.Task;

namespace TM.ServiceLogic.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // 1. User Mappings 
            CreateMap<User, AuthResponse>()
                .ForMember(dest => dest.Token, opt => opt.Ignore());
            CreateMap<User, UserResponse>();

            // 2. Task Mappings
            CreateMap<TaskEntity, TaskResponse>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.AssignedUserName,
                           opt => opt.MapFrom(src => src.AssignedUser != null ? src.AssignedUser.Username : "Unassigned"))
                .ForMember(dest => dest.DueDate, opt => opt.MapFrom(src => src.DueDate));

            CreateMap<TaskCreateRequest, TaskEntity>();
            CreateMap<TaskUpdateRequest, TaskEntity>();

            // 3. Comment Mappings
            CreateMap<Comment, CommentResponse>()
                .ForMember(dest => dest.AuthorName, opt => opt.MapFrom(src => src.User != null ? src.User.Username : "Unknown"));
        }
    }
}