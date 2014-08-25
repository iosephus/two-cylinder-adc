using System;
using System.Security.Cryptography;

// MersenneTwister.cs
// Mersenne Twister random number generator -- a C# class MTRand
// Based on code by Makoto Matsumoto, Takuji Nishimura, Shawn Cokus and Richard J. Wagner

// The Mersenne Twister is an algorithm for generating random numbers.  It
// was designed with consideration of the flaws in various other generators.
// The period, 2^19937-1, and the order of equidistribution, 623 dimensions,
// are far greater.  The generator is also fast; it avoids multiplication and
// division, and it benefits from caches and pipelines.  For more information
// see the inventors' web page at http://www.math.sci.hiroshima-u.ac.jp/~m-mat/MT/emt.html

// Reference
// M. Matsumoto and T. Nishimura, "Mersenne Twister: A 623-Dimensionally
// Equidistributed Uniform Pseudo-Random Number Generator", ACM Transactions on
// Modeling and Computer Simulation, Vol. 8, No. 1, January 1998, pp 3-30.

// Copyright (C) 1997 - 2002, Makoto Matsumoto and Takuji Nishimura,
// Copyright (C) 2000 - 2003, Richard J. Wagner
// Copyright (C) 2005 - 2014, Ignacio Rodriguez
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
//
//   1. Redistributions of source code must retain the above copyright
//      notice, this list of conditions and the following disclaimer.
//
//   2. Redistributions in binary form must reproduce the above copyright
//      notice, this list of conditions and the following disclaimer in the
//      documentation and/or other materials provided with the distribution.
//
//   3. The names of its contributors may not be used to endorse or promote
//      products derived from this software without specific prior written
//      permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

//
//     When you use this, send an email to: m-mat@math.sci.hiroshima-u.ac.jp
//     with an appropriate reference to your work.
//
// It would be nice to CC: wagnerr@umich.edu and Cokus@math.washington.edu
// when you write.

class MTRand {
	
	const int N = 624;       // length of state vector

	const int M = 397;       // period parameter

	uint[] state;            // internal state
	int nState;              // number of values used
	bool haveNextGaussian;
	double nextGaussian;
	
	static object staticLock = new object();
	
	// Not thread safe. Each thread should have its own MTRand object.
	// Only MTRand constructor is thread safe.
	
	public MTRand() {
		lock (staticLock) {
			RNGCryptoServiceProvider rnd = new RNGCryptoServiceProvider();
			state = new uint[N];
			haveNextGaussian = false;
			nextGaussian = 0;
			uint[] se = new uint[N];
			byte[] a = new byte[4];
			for (int i = 0; i < N; i++) {
				rnd.GetBytes(a);
				se[i] = (uint) ((uint)a[0] + (((uint)a[1]) << 8) + (((uint)a[2]) << 16) + (((uint)a[3]) << 24));
			}
			seed(se);
		}
	}
	
	// Returns a normally distributed random variable with mean 0 and standard deviation 1.
	// Uses the polar form of the Box - Muller transformation

	public double gaussian() {
		const double kran = 2.0 / 4294967295.0;
		if (haveNextGaussian) {
			haveNextGaussian = false;
			return nextGaussian;
		}
		double x, y, z, r;
		do {
			x = kran * randInt() - 1.0;
			y = kran * randInt() - 1.0;
			r = x * x + y * y;
		} while ((r >= 1) || (r == 0));
		z = Math.Sqrt(-2.0 * Math.Log(r) / r);
		haveNextGaussian = true;
		nextGaussian = x * z;
		return y * z;
	}


	public uint randInt() {
		// Pull a 32-bit integer from the generator state
		// Every other access function simply transforms the numbers extracted here
	
		if( nState == N ) {
			reload();
		}
	
		uint s1 = state[nState++];
		s1 ^= (s1 >> 11);
		s1 ^= (s1 <<  7) & 0x9d2c5680U;
		s1 ^= (s1 << 15) & 0xefc60000U;
		return ( s1 ^ (s1 >> 18) );
	}

	public void seed( uint[] bigSeed ) {
		// Seed the generator with an array of uint
		// There are 2^19937-1 possible initial states.  This function allows
		// all of those to be accessed by providing at least 19937 bits (with a
		// seed length of N = 624 uint).
		int i, j, k;
		state[0] = 19650218U;
		for (i = 1; i < N; i++) {
			state[i] = (uint) (1812433253U * (state[i - 1] ^ (state[i - 1] >> 30)) + i);
		}
		i = 1;
		j = 0;
		k = ( N > bigSeed.Length ? N : bigSeed.Length );
		for( ; k > 0; --k ) {
			state[i] = state[i] ^ ( (state[i - 1] ^ (state[i - 1] >> 30)) * 1664525U );
			state[i] = (uint) (state[i] + bigSeed[j] + j);
			i++;
			j++;
			if( i >= N ) { 
				state[0] = state[N - 1];
				i = 1;
			}
			if (j >= bigSeed.Length) {
				j = 0;
			}
		}
		for( k = N - 1; k > 0; --k ) {
			state[i] = state[i] ^ ( (state[i - 1] ^ (state[i - 1] >> 30)) * 1566083941U );
			state[i] = (uint) (state[i] -i);
			i++;
			if( i >= N ) { 
				state[0] = state[N - 1];
				i = 1;
			}
		}
		state[0] = 0x80000000U;  // MSB is 1, assuring non-zero initial array
		reload();
	}
	
	
	void reload() {
		
		uint y;
		int i;
	
		for( i = 0; i < N; i++ ) {
			y = (state[i] & 0x80000000U) | (state[(i + 1) % N] & 0x7fffffffU);
			state[i] = state[(i + M) % N] ^ (y >> 1) ^ (((y & 0x01) == 0) ? 0x00 : 0x9908b0dfU);
		}
		nState = 0;
	}
}

