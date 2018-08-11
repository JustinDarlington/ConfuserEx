﻿using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using dnlib.DotNet.Writer;

namespace Confuser.Core.Services {
	/// <summary>
	///     A seeded SHA256 PRNG.
	/// </summary>
	internal sealed class RandomGenerator : IRandomGenerator {
		/// <summary>
		///     The prime numbers used for generation
		/// </summary>
		static readonly byte[] primes = { 7, 11, 23, 37, 43, 59, 71 };

		readonly SHA256Managed sha256 = new SHA256Managed();
		int mixIndex;
		byte[] state; //32 bytes
		int stateFilled;

		/// <summary>
		///     Initializes a new instance of the <see cref="RandomGenerator" /> class.
		/// </summary>
		/// <param name="seed">The seed.</param>
		internal RandomGenerator(byte[] seed) {
			state = (byte[])seed.Clone();
			stateFilled = 32;
			mixIndex = 0;
		}

		/// <summary>
		///     Creates a seed buffer.
		/// </summary>
		/// <param name="seed">The seed data.</param>
		/// <returns>The seed buffer.</returns>
		internal static byte[] Seed(string seed) {
			byte[] ret;
			if (!string.IsNullOrEmpty(seed))
				ret = Utils.SHA256(Encoding.UTF8.GetBytes(seed));
			else
				ret = Utils.SHA256(Guid.NewGuid().ToByteArray());

			for (int i = 0; i < 32; i++) {
				ret[i] *= primes[i % primes.Length];
				ret = Utils.SHA256(ret);
			}
			return ret;
		}

		/// <summary>
		///     Refills the state buffer.
		/// </summary>
		void NextState() {
			for (int i = 0; i < 32; i++)
				state[i] ^= primes[mixIndex = (mixIndex + 1) % primes.Length];
			state = sha256.ComputeHash(state);
			stateFilled = 32;
		}

		/// <summary>
		///     Fills the specified buffer with random bytes.
		/// </summary>
		/// <param name="buffer">The buffer.</param>
		/// <param name="offset">The offset of buffer to fill in.</param>
		/// <param name="length">The number of random bytes.</param>
		/// <exception cref="System.ArgumentNullException"><paramref name="buffer" /> is <c>null</c>.</exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		///     <paramref name="offset" /> or <paramref name="length" /> is less than 0.
		/// </exception>
		/// <exception cref="System.ArgumentException">Invalid <paramref name="offset" /> or <paramref name="length" />.</exception>
		public void NextBytes(byte[] buffer, int offset, int length) {
			if (buffer == null)
				throw new ArgumentNullException("buffer");
			if (offset < 0)
				throw new ArgumentOutOfRangeException("offset");
			if (length < 0)
				throw new ArgumentOutOfRangeException("length");
			if (buffer.Length - offset < length)
				throw new ArgumentException("Invalid offset or length.");

			while (length > 0) {
				if (length >= stateFilled) {
					Buffer.BlockCopy(state, 32 - stateFilled, buffer, offset, stateFilled);
					offset += stateFilled;
					length -= stateFilled;
					stateFilled = 0;
				}
				else {
					Buffer.BlockCopy(state, 32 - stateFilled, buffer, offset, length);
					stateFilled -= length;
					length = 0;
				}
				if (stateFilled == 0)
					NextState();
			}
		}

		/// <summary>
		///     Returns a random byte.
		/// </summary>
		/// <returns>Requested random byte.</returns>
		public byte NextByte() {
			byte ret = state[32 - stateFilled];
			stateFilled--;
			if (stateFilled == 0)
				NextState();
			return ret;
		}

		/// <summary>
		///     Gets a buffer of random bytes with the specified length.
		/// </summary>
		/// <param name="length">The number of random bytes.</param>
		/// <returns>A buffer of random bytes.</returns>
		public byte[] NextBytes(int length) {
			var ret = new byte[length];
			NextBytes(ret, 0, length);
			return ret;
		}

		/// <summary>
		///     Returns a random boolean value.
		/// </summary>
		/// <returns>Requested random boolean value.</returns>
		public bool NextBoolean() {
			byte s = state[32 - stateFilled];
			stateFilled--;
			if (stateFilled == 0)
				NextState();
			return s % 2 == 0;
		}
	}

	/// <summary>
	///     Implementation of <see cref="IRandomService" />.
	/// </summary>
	internal class RandomService : IRandomService {
		readonly byte[] seed; //32 bytes

		/// <summary>
		///     Initializes a new instance of the <see cref="RandomService" /> class.
		/// </summary>
		/// <param name="seed">The project seed.</param>
		public RandomService(string seed) {
			this.seed = RandomGenerator.Seed(seed);
		}

		/// <inheritdoc />
		public IRandomGenerator GetRandomGenerator(string id) {
			if (string.IsNullOrEmpty(id))
				throw new ArgumentNullException("id");
			byte[] newSeed = seed;
			byte[] idHash = Utils.SHA256(Encoding.UTF8.GetBytes(id));
			for (int i = 0; i < 32; i++)
				newSeed[i] ^= idHash[i];
			return new RandomGenerator(Utils.SHA256(newSeed));
		}
	}
}
