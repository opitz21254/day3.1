namespace day3InClass;

class Course
{
    public Dictionary<int, int> course_availseats = new();
    public Dictionary<int, object> courseLocks = new();
    public readonly object seatLock = new();

    public Course(int courses = 100, int availseats = 10_000)
    {
        for (int i = 1; i <= courses; i++)
        {
            course_availseats[i] = availseats;
            courseLocks[i] = new object();
        }
    }
}

class Student
{
    public int id;
    public List<int> courses;

    public Student(int in_id)
    {
        id = in_id;
        courses = new();
    }

    public void SelectCoursesForRegistration(int num)
    {
        while (courses.Count < num)
        {
            int pick = Random.Shared.Next(100) + 1;
            if (!courses.Contains(pick))
                courses.Add(pick);
        }
    }

    public bool RegisterMyCourses()
    {
        if (Program.causeDeadlock)
            return RegisterDeadlock();
        else
            return RegisterNoDeadlock();
    }

    bool RegisterDeadlock()
    {
        bool allRegistered = true;

        foreach (var c in courses)
        {
            Monitor.Enter(Program.c.courseLocks[c]);
            Thread.Sleep(1);
        }

        foreach (var c in courses)
        {
            if (Program.c.course_availseats[c] > 0)
                Program.c.course_availseats[c]--;
            else
                allRegistered = false;
        }

        foreach (var c in courses)
        {
            Monitor.Exit(Program.c.courseLocks[c]);
        }

        return allRegistered;
    }

    bool RegisterNoDeadlock()
    {
        bool allRegistered = true;

        List<int> lockOrder = courses.OrderBy(x => x).ToList();

        foreach (var c in lockOrder)
        {
            Monitor.Enter(Program.c.courseLocks[c]);
        }

        try
        {
            foreach (var c in courses)
            {
                if (Program.c.course_availseats[c] > 0)
                    Program.c.course_availseats[c]--;
                else
                    allRegistered = false;
            }
        }
        finally
        {
            for (int i = lockOrder.Count - 1; i >= 0; i--)
            {
                Monitor.Exit(Program.c.courseLocks[lockOrder[i]]);
            }
        }

        return allRegistered;
    }
}

class Program
{
    public static Course c = new();

    public static bool causeDeadlock = false;

    static void Main(string[] args)
    {
        if (causeDeadlock)
            Console.WriteLine("DEADLOCK MODE on — if this stops printing, the threads are stuck");
        else
            Console.WriteLine("SAFE MODE on — locking courses from low id to high id");

        List<Thread> myThreads = new();
        for (int i = 1; i < 200; i++)
        {
            myThreads.Add(new Thread(() =>
            {
                for (int i = 1; i <= 1000; i++)
                {
                    Student s = new Student(i);
                    s.SelectCoursesForRegistration(5);
                    if (s.RegisterMyCourses() ==false)
                        Console.WriteLine($"Student {s.id} failed to register course list");
                }
            }));
        }

        foreach (var x in myThreads)
        {
            x.Start();
        }

        foreach (var x in myThreads)
        {
            x.Join();
        }
        Console.WriteLine("All seats assigned");
        foreach (var x in c.course_availseats)
        {
            Console.WriteLine($"Course {x.Key} has {x.Value} seats still available");
        }
    }
}
