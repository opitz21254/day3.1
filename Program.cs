namespace day3InClass;

/*
 * Wednesday demo notes
 * --------------------
 * 1) Threads
 *    Defined: each Thread is created with new Thread(() => { ... }) and stored
 *    in myThreads. The lambda is the work: 1000 students pick 5 courses and register.
 *    Started: after ALL threads are created, a second loop calls Start() on each.
 *    Done: a third loop calls Join() on each thread. Join() blocks until that
 *    thread has finished, so we only print leftover seats after everyone is done.
 *    Why two loops (start vs join): if you Start() then Join() in the SAME loop,
 *    thread 1 would run all the way to finish before thread 2 even starts.
 *    You would get no overlap. Create all -> start all -> join all is how they
 *    actually run at the same time.
 *
 * 2) Deadlock (set causeDeadlock = true)
 *    What it took: one lock object per course, then grab ALL of a student's
 *    course locks BEFORE touching seats, in the order the student picked
 *    (random). Hold lock A while waiting for lock B.
 *    What determines a deadlock: the 4 Coffman conditions. The one we create
 *    is circular wait (plus mutex, hold-and-wait, and no preemption).
 *    Example: student A picked 12 then 44 (holds 12, waits 44).
 *             student B picked 44 then 12 (holds 44, waits 12). Stuck forever.
 *
 * 3) Fix (causeDeadlock = false) — still uses the same per-course locks
 *    Options:
 *      a) go back to one global lock (no deadlock, less concurrency)
 *      b) lock ordering / hierarchy — always lock lower course id first
 *      c) Monitor.TryEnter with a timeout, then back off and retry
 *      d) only hold one course lock at a time (but then "all 5 courses" is racy)
 *    Coded: option b. Sort the course ids, lock low -> high, then decrement.
 *    Why it can't deadlock: every thread acquires locks in the SAME total
 *    order. A wait-for cycle cannot form, so circular wait is broken.
 */

class Course
{
    public Dictionary<int, int> course_availseats = new();
    public Dictionary<int, object> courseLocks = new();   // one lock per course
    public readonly object seatLock = new();              // leftover from class (not used now)

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
            int pick = Random.Shared.Next(100) + 1; // 1-100
            if (!courses.Contains(pick))            // don't pick the same course twice
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

    // grabs locks in whatever order the student picked courses (random)
    // two students with opposite orders can circular-wait -> deadlock
    bool RegisterDeadlock()
    {
        bool allRegistered = true;

        foreach (var c in courses)
        {
            Monitor.Enter(Program.c.courseLocks[c]);
            Thread.Sleep(1);   // makes the deadlock show up faster in the demo
        }

        foreach (var c in courses)
        {
            if (Program.c.course_availseats[c] > 0)
                Program.c.course_availseats[c]--;
            else
                allRegistered = false;          // course is full
        }

        foreach (var c in courses)
        {
            Monitor.Exit(Program.c.courseLocks[c]);
        }

        return allRegistered;
    }

    // SAME locks, but always acquired in course-id order (low to high)
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
                    allRegistered = false;          // course is full
            }
        }
        finally
        {
            // unlock in reverse of the order we locked
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

    // DEMO SWITCH: true  = hang (deadlock)     false = lock-ordering fix
    public static bool causeDeadlock = false;

    static void Main(string[] args)
    {
        if (causeDeadlock)
            Console.WriteLine("DEADLOCK MODE on — if this stops printing, the threads are stuck");
        else
            Console.WriteLine("SAFE MODE on — locking courses from low id to high id");

        List<Thread> myThreads = new();
        for (int i = 1; i < 200; i++)   // meant to be 200 threads
        {
            // 1) Create a thread to do N (1000) student registrations
            myThreads.Add(new Thread(() =>
            {
                for (int i = 1; i <= 1000; i++)
                {
                    Student s = new Student(i);          // create student
                    s.SelectCoursesForRegistration(5);   // Student.SelectCoursesForRegistration
                    if (s.RegisterMyCourses() ==false)               // Student.RegisterMyCourses
                        Console.WriteLine($"Student {s.id} failed to register course list");
                }
            }));
        }

        // start EVERY thread first so they overlap
        foreach (var x in myThreads)
        {
            x.Start();
        }

        // then wait for ALL of them (Join = block until that thread is done)
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
