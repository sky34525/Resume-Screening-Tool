namespace ResumeScreening_Desktop.Models;

public sealed class ResumeProfile
{
    public string Name { get; init; }
    public string Age { get; init; }
    public string Gender { get; init; }
    public string Education { get; init; }
    public string School { get; init; }
    public string Major { get; init; }

    public ResumeProfile(string name, string age, string gender, string education, string school, string major)
    {
        Name = name;
        Age = age;
        Gender = gender;
        Education = education;
        School = school;
        Major = major;
    }
}
