﻿using Accord.Controls;
using Accord.Math;
using Accord.Math.Optimization.Losses;
using Accord.Statistics;
using Accord.Statistics.Models.Regression.Linear;
using Accord.Statistics.Visualizations;
using Deedle;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ml_csharp_lesson5
{
    /// <summary>
    /// The main application class.
    /// </summary>
    public class Program
    {
        // helper method to generate ranges
        private static IEnumerable<(int Min, int Max)> RangeFromTo(int from, int to, int step = 1)
        {
            for (int i = from; i <= to; i += step)
                yield return (Min: i, Max: i + step);
        }

        /// <summary>
        /// The main application entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static void Main(string[] args)
        {
            // get data
            Console.WriteLine("Loading data....");
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..");
            path = Path.Combine(path, "..");
            path = Path.Combine(path, "california_housing.csv");
            var housing = Frame.ReadCsv(path, separators: ",");
            housing = housing.Where(kv => ((decimal)kv.Value["median_house_value"]) < 500000);

            // shuffle row indices
            var rnd = new Random();
            var indices = Enumerable.Range(0, housing.Rows.KeyCount).OrderBy(v => rnd.NextDouble());

            // shuffle the frame using the indices
            housing = housing.IndexRowsWith(indices).SortRowsByKey();

            // convert the house value range to thousands
            housing["median_house_value"] /= 1000;

            // create the rooms_per_person feature
            housing.AddColumn("rooms_per_person",
               (housing["total_rooms"] / housing["population"]).Select(v => v.Value <= 4.0 ? v.Value : 4.0));

            // bin the longitude in 12 buckets
            var vectors_long =
                from l in housing["longitude"].Values
                select Vector.Create<double>(
                    1,
                    (from b in RangeFromTo(-125, -114, 1)
                     select l >= b.Min && l < b.Max).ToArray());

            // bin the latitude in 12 buckets
            var vectors_lat =
                from l in housing["latitude"].Values
                select Vector.Create<double>(
                    1,
                    (from b in RangeFromTo(32, 43, 1)
                     select l >= b.Min && l < b.Max).ToArray());

            // get the outer product of the longitude and latitude vectors
            var vectors_cross =
                vectors_long.Zip(vectors_lat, (lng, lat) => lng.Outer(lat));

            // build one-hot columns out of the feature cross vectors
            for (var i = 0; i < 12; i++)
                for (var j = 0; j < 12; j++)
                    housing.AddColumn($"location {i},{j}", from v in vectors_cross select v[i, j]);

            // print the data frame
            housing.Print();

            // create training, validation, and test frames
            var training = housing.Rows[Enumerable.Range(0, 12000)];
            var validation = housing.Rows[Enumerable.Range(12000, 2500)];
            var test = housing.Rows[Enumerable.Range(14500, 2500)];

            // set up model columns
            var columns = (from i in Enumerable.Range(0, 12)
                           from j in Enumerable.Range(0, 12)
                           select $"location {i},{j}").ToList();
            columns.Add("median_income");
            columns.Add("rooms_per_person");

            // train the model
            var learner = new OrdinaryLeastSquares() { IsRobust = true };
            var regression = learner.Learn(
                training.Columns[columns].ToArray2D<double>().ToJagged(),  // features
                training["median_house_value"].Values.ToArray());          // labels

            // display training results
            Console.WriteLine("TRAINING RESULTS");
            Console.WriteLine($"Weights:     {regression.Weights.ToString<double>("0.00")}");
            Console.WriteLine($"Intercept:   {regression.Intercept}");
            Console.WriteLine();

            // validate the model
            var predictions = regression.Transform(
                validation.Columns[columns].ToArray2D<double>().ToJagged());

            // display validation results
            var labels = validation["median_house_value"].Values.ToArray();
            var rmse = Math.Sqrt(new SquareLoss(labels).Loss(predictions));
            Console.WriteLine("VALIDATION RESULTS");
            Console.WriteLine($"RMSE:        {rmse:0.00}");
            Console.WriteLine();

            // test the model
            var predictions_test = regression.Transform(
                test.Columns[columns].ToArray2D<double>().ToJagged());

            // display test results
            var labels_test = test["median_house_value"].Values.ToArray();
            rmse = Math.Sqrt(new SquareLoss(labels_test).Loss(predictions_test));
            Console.WriteLine("TEST RESULTS");
            Console.WriteLine($"RMSE:        {rmse:0.00}");
            Console.WriteLine();

            Console.ReadLine();
        }
    }
}
