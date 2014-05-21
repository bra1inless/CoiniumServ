﻿/*
 *   CoiniumServ - crypto currency pool software - https://github.com/CoiniumServ/CoiniumServ
 *   Copyright (C) 2013 - 2014, Coinium Project - http://www.coinium.org
 *
 *   This program is free software: you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation, either version 3 of the License, or
 *   (at your option) any later version.
 *
 *   This program is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU General Public License for more details.
 *
 *   You should have received a copy of the GNU General Public License
 *   along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Threading;
using Coinium.Common.Extensions;
using Coinium.Core.Coin.Algorithms;
using Coinium.Core.Coin.Daemon;
using Coinium.Core.Coin.Helpers;
using Coinium.Core.Coin.Transactions;
using Coinium.Core.Crypto;
using Coinium.Core.Mining.Jobs;
using Coinium.Core.Mining.Miner;
using Coinium.Core.Server.Stratum;
using Coinium.Core.Server.Stratum.Notifications;
using Coinium.Net.Server.Sockets;
using Org.BouncyCastle.Math;
using Serilog;

namespace Coinium.Core.Mining
{
    /// <summary>
    /// Miner manager that manages all connected miners over different ports.
    /// </summary>
    public class MiningManager
    {
        // dependencies.
        private readonly IJobManager _jobManager;

        private int _counter; // counter for assigining unique id's to miners.


        private BigInteger diff1;
       
        public MiningManager(IJobManager jobManager)
        {
            // setup our dependencies.
            this._jobManager = jobManager;

            this.diff1 = new BigInteger("00000000ffff0000000000000000000000000000000000000000000000000000", 16);

            Log.Verbose("MinerManager() init..");
        }


        public bool ProcessShare(StratumMiner miner, string jobId, string extraNonce2, string nTimeString, string nonceString)
        {
            // check if the job exists
            var id = Convert.ToUInt64(jobId, 16);
            var job = this._jobManager.GetJob(id);                

            if (job == null)
            {
                Log.Warning("Job doesn't exist: {0}", id);
                return false;
            }

            if (nTimeString.Length != 8)
            {
                Log.Warning("Incorrect size of nTime");
                return false;
            }

            if (nonceString.Length != 8)
            {
                Log.Warning("incorrect size of nonce");
                return false;
            }

            var nTime = Convert.ToUInt32(nTimeString, 16);
            var nonce = Convert.ToUInt32(nonceString, 16);

            var coinbase = Serializers.SerializeCoinbase(job, ExtraNonce.Instance.Current, Convert.ToUInt32(extraNonce2, 16));
            var coinbaseHash = this.HashCoinbase(coinbase);

            var merkleRoot = job.MerkleTree.WithFirst(coinbaseHash).ReverseBytes();

            var algorithm = HashAlgorithmFactory.Get("scrypt");

            var header = Serializers.SerializeHeader(job, merkleRoot, nTime, nonce);
            var headerHash = algorithm.Hash(header);
            var headerValue = new BigInteger(headerHash.ToHexString(), 16);

            var shareDiff = diff1.Divide(headerValue).Multiply(BigInteger.ValueOf(algorithm.Multiplier));
            var blockDiffAdjusted = 16 * algorithm.Multiplier;

            var target = new BigInteger(job.NetworkDifficulty, 16);
            if (target.Subtract(headerValue).IntValue > 0) // Check if share is a block candidate (matched network difficulty)
            {
                var block = Serializers.SerializeBlock(job, header, coinbase).ToHexString();
            }
            else // invalid share.
            {

            }

            return true;
        }        

        private Hash HashCoinbase(byte[] coinbase)
        {
            return coinbase.DoubleDigest();
        }
    }
}
