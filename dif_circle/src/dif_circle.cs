using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

public delegate void MyDelegate();

class TimeElapsed {
	DateTime started;
	
	public TimeElapsed() {
		started = DateTime.Now;
	}
	
	public string elapsed(int nRead, int nTotal) {
		TimeSpan t = DateTime.Now - started;
		double objective = (nTotal - nRead) * t.TotalSeconds / nRead;
		int obj;
		int percent = (int) Math.Round(100 * ((double) nRead) / nTotal);
		string s = String.Format("Done: {0}%. Remaining: ", percent);
		if (objective < 60) {
			obj = (int) (5 * Math.Ceiling(objective / 5));
			return String.Concat(s, obj.ToString(), " seconds.");
		}
		if (objective < 3600) {
			obj = (int) Math.Ceiling(objective / 60);
			return String.Concat(s, obj.ToString(), " minutes.");
		}
		if (objective < 36000) {
			obj = (int) Math.Ceiling(objective / 60);
			int ho = obj / 60;
			int mi = obj % 60;
			string s1, s2;
			if (ho > 1) {
				s1 = String.Concat(ho.ToString(), " hours");
			}
			else {
				s1 = "1 hour";
			}
			if (mi > 1) {
				s2 = String.Concat(" and ", mi.ToString(), " minutes");
			}
			else if (mi == 1) {
				s2 = " and 1 minute";
			}
			else {
				s2 = "";
			}
			return String.Concat(s, s1, s2, ".");
		}
		obj = (int) Math.Ceiling(objective / 3600);
		return String.Concat(s, obj.ToString(), " hours.");
	}
}

// Spin structure. Contains phase information for three different gradient shapes

struct spin_t {
	public double nmr_phase_delta;
	public double nmr_phase_square;
	public double nmr_phase_sin;
	public bool calculated;
}

class dif_circle : Form {

	spin_t[] spins;
	Label l;
	ProgressBar p;
	Button ok;
	string[] args;
	bool active;
	int spins_done;
	TimeElapsed te;
	StreamWriter log;
	System.Windows.Forms.Timer myTimer;
	uint num_spins;
	uint num_steps;
	double radius;
	double dt;
	double mean_disp_sqrt2;
	double sin_phase_step;
	
	object objLock = new object();

	static string datadir = Path.Combine(Application.StartupPath, "data");
	static string logdir = Path.Combine(Application.StartupPath, "logs");
	static string output_file;
	static string temp_file;
	static string log_file;


	public dif_circle(string[] argv) {
		args = argv;
		l = new Label();
		l.Text = "0000 0000 0000 0000 0000 0000 0000 0000 0000 0000 0000 0000 0000 0000 0000 0000";
		p = new ProgressBar();
		l.Width = l.PreferredWidth;
		l.Top = 10;
		l.Left = 10;
		p.Top = 40;
		p.Width = l.Width;
		p.Left = l.Left;
		Width = l.Width + 50;
		Height = 150;
		ok = new Button();
		ok.Text = "Cancel";
		ok.Size = ok.PreferredSize;
		ok.Left = (Width - ok.Width) / 2;
		ok.Top = 80;
		ok.Click += new EventHandler(this.ok_clicked);
		Controls.AddRange(new Control[] {l, p, ok});
		Text = "Calculating. Please, wait.";
		l.Text = "";
		active = true;
		myTimer = new System.Windows.Forms.Timer();
		myTimer.Tick += new EventHandler(AlarmRang);
		myTimer.Interval = 15000;
		myTimer.Start();
		Thread t = new Thread(new ThreadStart(this.exec));
		t.Priority = ThreadPriority.BelowNormal;
		t.Start();
	}
	
	void ok_clicked(object sender, EventArgs e) {
		lock (objLock) {
			active = false;
		}
	}
	
	protected override void OnFormClosing(FormClosingEventArgs e) {
		lock (objLock) {
			active = false;
		}
	}
	
	void exec() {
		
		/* Physical parameters */
		double diff_coeff, diff_time;
		double adim_ratio;
	
		/* Threading parameters */
		uint num_threads;
		Thread[] thread;
	
		/* IO parameters */
		BinaryWriter fp;
	
		/* Logging parameters */
		DateTime timer_start_calc;
		DateTime timer_end_calc;

		uint i;

		bool prev_file = false;
		
		NumberFormatInfo nf = new CultureInfo("en-US").NumberFormat;
	
		num_spins = uint.Parse(args[0], nf);
		num_steps = uint.Parse(args[1], nf);
		num_threads = uint.Parse(args[2], nf);
		adim_ratio = double.Parse(args[3], nf);
		output_file = Path.Combine(Path.Combine(Application.StartupPath, "data"), String.Format("data_circle_{0}x{1}_{2}.bin", args[0], args[1], args[3]));
		temp_file = Path.Combine(Path.Combine(Application.StartupPath, "data"), String.Format("data_circle_{0}x{1}_{2}.tmp", args[0], args[1], args[3]));
		log_file = Path.Combine(Path.Combine(Application.StartupPath, "logs"), String.Format("run_circle_{0}x{1}_{2}.log", args[0], args[1], args[3]));
		
		if (File.Exists(temp_file)) {
			FileInfo fi = new FileInfo(temp_file);
			if ((fi.Length % 24) == 0) {
				num_spins = num_spins - ((uint)(fi.Length / 24));
				prev_file = true;
			}
		}
		
		/* Diffusion coefficient and time set to 1, relation to radius given by adim_ratio */
		diff_time = 1;
		diff_coeff = 1;
	
		dt = diff_time / num_steps;
		mean_disp_sqrt2 = Math.Sqrt(2.0 * diff_coeff * dt);
		sin_phase_step = 2.0 * Math.PI / num_steps;
	
		radius = adim_ratio * Math.Sqrt(2 * diff_coeff * diff_time);
		
		p.Minimum = 0;
		p.Maximum = (int)num_spins;
		
		log = new StreamWriter(new FileStream(log_file, FileMode.Append, FileAccess.Write, FileShare.None));
		print("");

		/* Allocate memory for threads */
		thread = new Thread[num_threads];

		/* Allocate memory for spin structures */
		spins = new spin_t[num_spins];
		
		for (i = 0; i < num_spins; i++) {
			spins[i].calculated = false;
		}

		spins_done = 0;
	
		print("Computing with {0} thread(s), {1} steps for {2} spins.\n", num_threads, num_steps, num_spins);
	
		print("Diffusion time {0}", diff_time);
		print("Free Diffusion {0}", diff_coeff);
		print("Radius {0}", radius);
		
		timer_start_calc = DateTime.Now;
		
		te = new TimeElapsed();
	
		/* Create independent threads each of which will execute function */
		for (i = 0; i < num_threads; i++) {
			
			thread[i] = new Thread(new ThreadStart(this.walk_spins));
			thread[i].Priority = ThreadPriority.BelowNormal;
		}
		
		/* Start threads */
		for (i = 0; i < num_threads; i++) {
			thread[i].Start();
		}

		/* Wait for all threads to finish */
		for (i = 0; i < num_threads; i++) {
			thread[i].Join();
		}
	
		bool xactive = false;
		
		lock (objLock) {
			xactive = active;
		}
		
		timer_end_calc = DateTime.Now;
		
		if (xactive) {
			print("Computing is finished, took {0} second(s).", (timer_end_calc - timer_start_calc).TotalSeconds);
		}
		else {
			print("Computing was stopped, took {0} second(s) for {1} spins.", (timer_end_calc - timer_start_calc).TotalSeconds, spins_done);
		}

		print("Writing binary data to disk.");
		
		if (prev_file) {
			fp = new BinaryWriter(new FileStream(temp_file, FileMode.Append, FileAccess.Write, FileShare.None));
		}
		else {
			fp = new BinaryWriter(new FileStream(temp_file, FileMode.Create, FileAccess.Write, FileShare.None));
		}
		
		int spins_written = 0;
	
		for (i = 0; i < num_spins; i++) {
			
			if (spins[i].calculated) {
	
				fp.Write(spins[i].nmr_phase_delta);
				fp.Write(spins[i].nmr_phase_square);
				fp.Write(spins[i].nmr_phase_sin);
				
				spins_written++;
			}
		}
	
		fp.Close();
	
		print("{0} bytes of data written to \"{1}\".", 24 * spins_written, temp_file);
		
		if (spins_done != spins_written) {
			print("Done {0} spins. Written {1} spins.", spins_done, spins_written);
		}
		
		if (xactive) {
			File.Move(temp_file, output_file);
			print("Moving data from {0} to {1}", temp_file, output_file);
		}
	
		print("Exiting.");
		
		log.Close();
		
		Application.Exit();
		
	}
	
	void AlarmRang(object sender, EventArgs e) {
		lock (objLock) {
			p.Value = spins_done;
		}
		l.Text = te.elapsed(p.Value, p.Maximum);
	}

	void walk_spins() {
		MTRand rand = new MTRand();
	
		for (uint i = 0; i < num_spins; i++) {
		
			calc_spin(rand, i);
	
		}
	}

	void calc_spin(MTRand rand, uint i) {
	
		lock (objLock) {
			if (!active) {
				return;
			}
			if (spins[i].calculated) {
				return;
			}
			spins[i].calculated = true;
			spins_done++;
		}
	
		uint j;
		double x, x_next, y, y_next;
		double square_radius;
		double nmr_phase_delta, nmr_phase_square, nmr_phase_sin;
		double conv_uint32_0tod;
	
		/* Get simul info */
		
		square_radius = radius * radius;
		conv_uint32_0tod = 2.0 * radius / 4294967295.0;
	
		do {
			x = conv_uint32_0tod * rand.randInt() - radius;
			y = conv_uint32_0tod * rand.randInt() - radius;
	
		} while ( x * x + y * y > square_radius );

		nmr_phase_delta = x;
		nmr_phase_square = 0;
		nmr_phase_sin = 0;

		for (j = 0; j < num_steps; j++) {

			if (j < (num_steps / 2)) {

				nmr_phase_square = nmr_phase_square + x;
			} 
			else {

				nmr_phase_square = nmr_phase_square - x;
			}

			nmr_phase_sin = nmr_phase_sin + x * Math.Sin(sin_phase_step * j);

			do {
				x_next = x + mean_disp_sqrt2 * rand.gaussian();
				y_next = y + mean_disp_sqrt2 * rand.gaussian();
			} while (x_next * x_next + y_next * y_next > square_radius);

			x = x_next;
			y = y_next;
		}

		nmr_phase_delta = nmr_phase_delta - x;

		spins[i].nmr_phase_delta = nmr_phase_delta;
		spins[i].nmr_phase_square = nmr_phase_square * dt;
		spins[i].nmr_phase_sin = nmr_phase_sin * dt;
		
	}

	static void Main(string[] args) {
		if (args.Length < 4) {
			Console.WriteLine("Usage: dif_circle <num_spins> <num_steps> <num_threads> <adim_ratio>");
			return;
		}
		output_file = Path.Combine(datadir, String.Format("data_circle_{0}x{1}_{2}.bin", args[0], args[1], args[3]));
		temp_file = Path.Combine(datadir, String.Format("data_circle_{0}x{1}_{2}.tmp", args[0], args[1], args[3]));
		log_file = Path.Combine(logdir, String.Format("run_circle_{0}x{1}_{2}.log", args[0], args[1], args[3]));
		if (!File.Exists(output_file)) {
			if (!Directory.Exists(datadir)) {
				Directory.CreateDirectory(datadir);
			}
			if (!Directory.Exists(logdir)) {
				Directory.CreateDirectory(logdir);
			}
			Application.Run(new dif_circle(args));
		}
	}	


	void print(string s, params object[] obj) {
		lock (objLock) {
			log.WriteLine("{0} : {1}", DateTime.Now.ToString(), String.Format(s, obj));
			log.Flush();
		}
	}
}
			