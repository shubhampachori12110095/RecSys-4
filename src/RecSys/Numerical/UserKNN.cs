﻿using MathNet.Numerics.LinearAlgebra.Double;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.Data.Text;
using System.Diagnostics;
using MathNet.Numerics.LinearAlgebra;
using RecSys.Core;

namespace RecSys.Numerical
{
    public static class UserKNN
    {
        #region UserKNN
        /// <summary>
        /// The user-based KNN collaborative filtering described in paper: 
        /// Resnick, P., et al., "GroupLens: an open architecture for collaborative filtering of netnews", 1994.
        /// Link: http://dx.doi.org/10.1145/192844.192905
        /// </summary>
        /// <param name="R_train"></param>
        /// <param name="R_unknown"></param>
        /// <param name="K"></param>
        /// <returns></returns>
        public static RatingMatrix PredictRatings(RatingMatrix R_train, RatingMatrix R_unknown, int K)
        {
            // Debug
            Debug.Assert(R_train.UserCount == R_unknown.UserCount);
            Debug.Assert(R_train.ItemCount == R_unknown.ItemCount);
            int cappedCount = 0, globalMeanCount = 0;

            // This matrix stores predictions
            RatingMatrix R_predicted = new RatingMatrix(R_unknown.UserCount, R_unknown.ItemCount);

            // Basic statistics from train set
            double globalMean = R_train.GetGlobalMean();
            Vector<double> userMeans = R_train.GetUserMeans();
            Vector<double> itemMeans = R_train.GetItemMeans();

            foreach (Tuple<int, Vector<double>> user in R_unknown.Users)
            {
                int userIndex = user.Item1;
                RatingVector userRatings = new RatingVector(R_train.GetRow(userIndex));
                RatingVector unknownRatings = new RatingVector(user.Item2);

                Utils.PrintEpoch("Predicting user/total", userIndex, R_train.UserCount);

                Dictionary<int, double> topKNeighbors = KNNCore.GetTopKNeighborsByUser(R_train.UserSimilarities, userIndex, K);

                double userMean = userMeans[userIndex];

                // Loop through each item to be predicted
                foreach (Tuple<int, double> unknownRating in unknownRatings.Ratings)
                {
                    int itemIndex = unknownRating.Item1;
                    double prediction;

                    // Compute the average rating on item iid given 
                    // by the top K neighbors. Each rating is offsetted by
                    // the neighbor's average and weighted by the similarity
                    double weightedSum = 0;
                    double weightSum = 0;
                    foreach (KeyValuePair<int, double> neighbor in topKNeighbors)
                    {
                        int neighborIndex = neighbor.Key;
                        double neighborSimilarity = neighbor.Value;
                        double neighborRating = R_train[neighborIndex, itemIndex];
                        if (neighborRating != 0)
                        {
                            // The weightSum serves as the normalization term
                            // it needs abs() because some metric such as Pearson 
                            // may produce negative weights
                            weightSum += neighborSimilarity;
                            weightedSum += (neighborRating - userMeans[neighborIndex]) * neighborSimilarity;
                        }
                    }
                    // A zero weightedSum means this is a cold item and global mean will be assigned by default
                    if (weightedSum != 0)
                    {
                        prediction = userMean + weightedSum / weightSum;
                    }
                    else
                    {
                        prediction = globalMean;
                        globalMeanCount++;
                    }

                    // Cap the ratings
                    if (prediction > Config.Ratings.MaxRating)
                    {
                        cappedCount++;
                        prediction = Config.Ratings.MaxRating;
                    }
                    if (prediction < Config.Ratings.MinRating)
                    {
                        cappedCount++;
                        prediction = Config.Ratings.MinRating;
                    }

                    R_predicted[userIndex, itemIndex] = prediction;
                }
            }
            Console.WriteLine("{0,-23} │ {1,13}", "# capped predictions", cappedCount);
            Console.WriteLine("{0,-23} │ {1,13}", "# default predictions", globalMeanCount);
            return R_predicted;
        }
        #endregion

    }
}