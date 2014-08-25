using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

class Test {

	// Checks the output of MersenneTwister.cs using the known published sequence
	// http://www.math.sci.hiroshima-u.ac.jp/~m-mat/MT/MT2002/CODES/mt19937ar.out

	static void Main() {
		int i;
		MTRand rand = new MTRand();
		rand.seed(new uint[] {0x123, 0x234, 0x345, 0x456});
		string output_file = Path.Combine(Application.StartupPath, "mt.cs.out");
		string s;
		using (StreamWriter log = new StreamWriter(new FileStream(output_file, FileMode.Create, FileAccess.Write, FileShare.None))) {
			log.WriteLine("mt.cs.out");
			log.WriteLine("Compare the output of MersenneTwister.cs with the published output sequence");
			log.WriteLine("http://www.math.sci.hiroshima-u.ac.jp/~m-mat/MT/MT2002/CODES/mt19937ar.out");
			log.WriteLine();
			log.WriteLine("1000 outputs of MTRand.randInt()");
			for (i = 0; i < 1000; i++) {
				s = rand.randInt().ToString("D10");
				if ((i % 5) == 4) {
					log.WriteLine("{0}", s);
				}
				else {
					log.Write("{0} ", s);
				}
			}
			log.WriteLine();
			log.WriteLine("1000 outputs of MTRand.randInt() / 4294967296");
			log.WriteLine("Corresponds to random variables uniformly distributed in [0, 1)");
			for (i = 0; i < 1000; i++) {
				s = (rand.randInt() / 4294967296.0).ToString("F8");
				if ((i % 5) == 4) {
					log.WriteLine("{0}", s);
				}
				else {
					log.Write("{0} ", s);
				}
			}
		}
	}	
}
			