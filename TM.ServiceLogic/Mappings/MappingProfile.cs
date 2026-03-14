using AutoMapper;
using TM.Contracts.Auth;
using TM.Contracts.Comments;
using TM.Contracts.Tasks;
using TM.Model.Entities;
// Explicitly alias Task to avoid conflict with System.Threading.Tasks
using TaskEntity = TM.Model.Entities.Task;

namespace TM.ServiceLogic.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // 1. User Mappings (Handles UserRole Enum to String)
            CreateMap<User, AuthResponse>()
                .ForMember(dest => dest.Token, opt => opt.Ignore());
            CreateMap<User, UserResponse>();

            // 2. Task Mappings (Handles Status Enum, Assigned Name, and DueDate)
            CreateMap<TaskEntity, TaskResponse>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.AssignedUserName,
                           opt => opt.MapFrom(src => src.AssignedUser != null ? src.AssignedUser.Username : "Unassigned"))
                .ForMember(dest => dest.DueDate, opt => opt.MapFrom(src => src.DueDate));

            // 3. Request Mappings (Converting incoming data to Entities)
            CreateMap<TaskCreateRequest, TaskEntity>();
            CreateMap<TaskUpdateRequest, TaskEntity>();

            // 4. Comment Mappings
            CreateMap<Comment, CommentResponse>()
                .ForMember(dest => dest.AuthorName, opt => opt.MapFrom(src => src.User != null ? src.User.Username : "Unknown"));
        }
    }
}