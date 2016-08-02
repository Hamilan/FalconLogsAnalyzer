using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FalconStudyDataAnalyzer
{
    class Program
    {
        const double FalconMinY = -0.06;
        const double LiftPercentToStart = 0.3;
        const double IddleDelta = 0.003;
        const int MinSecondsToStopLog = 20;
                
        static void Main(string[] args)
        {
            string username = "P03";
            string logsPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Dropbox\FalconStudyData\" + username + @"\";

            //The fixLogFilesV# methods should only be used if working on the original files labeled as broken under the data directory
            //As of December 1st, all logs V0 and V1 have been fixed. The next two methods should not be necessary anymore.
            //fixLogFilesV1(username,logsPath);
            //fixLogFilesV2(username, logsPath);
            //As of December 11, 2015 all logs V2 have been fixed. The next method should not be necessary anymore.
            //fixLogFilesV3(username, logsPath);
            //The Falcon Therapy Manager is now creating the log files as expected (V3)

            //trimLogs(username, logsPath);
            createReportBasedOnDate(username, logsPath);
            createReportPerGame(username, logsPath);
        }

        class DataSet {
            public double x, y, z;
            public bool buttonPressed;
            public string line;
            public DataSet(double x,double y, double z, bool buttonPressed, string line)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.buttonPressed = buttonPressed;
                this.line = line;
            }
        }

        //Removes idle time at the beginning and at the end of the file.
        //In detail: Loads the entire file in a Queue, skipping idle time at the beginning of the file.
        //Then goes through the Queue as in a stack (i.e. file in reverse), skipping idle time at the beginning of the stack (i.e. end of the file).
        private static void trimLogs(string username, string logsPath)
        {
            Console.WriteLine("Trimming logs in " + logsPath);
            
            string filenamesPattern = "GameLog_" + username + "_*.csv";
            List<string> filePaths = new List<string>(Directory.GetFiles(logsPath + "/", filenamesPattern));
            foreach (string fileIn in filePaths)
            {
                try
                {
                    StreamReader reader = new StreamReader(fileIn);
                    StreamWriter writer = new StreamWriter(fileIn.Substring(0, fileIn.Length - 4) + "t.csv");

                    writer.WriteLine(reader.ReadLine());    //Date and time
                    writer.WriteLine(reader.ReadLine());    //Game name
                    writer.WriteLine(reader.ReadLine());    //Username
                    writer.WriteLine(reader.ReadLine());    //Columns' titles

                    Stack<DataSet> linesStack = new Stack<DataSet>();
                    string[] data;
                    String line="";
                    try
                    {
                        double FalconX = 0, FalconY = 0, FalconZ = 0;
                        double minX = 999, maxX = 0, minY = 0, maxY = 0, minZ = 0, maxZ = 0; 
                            
                        bool trimmingStart = true;
                        while ((line = reader.ReadLine()) != null)
                        {
                            data = line.Split(',');
                            if (data.Count() < 14 || data[0] == "End of log")
                            {
                                break;
                            }
                            FalconX = float.Parse(data[1]);
                            FalconY = float.Parse(data[2]);
                            FalconZ = float.Parse(data[3]);
                            bool buttonPressed = bool.Parse(data[14].ToLower());

                            if(minX == 999)
                            {
                                minX = maxX = FalconX;
                                minY = maxY = FalconY;
                                minZ = maxZ = FalconZ;
                            }
                            if (FalconX < minX)
                                minX = FalconX;
                            if (FalconX > maxX)
                                maxX = FalconX;
                            if (FalconY < minY)
                                minY = FalconY;
                            if (FalconY > maxY)
                                maxY = FalconY;
                            if (FalconZ < minZ)
                                minZ = FalconZ;
                            if (FalconZ > maxZ)
                                maxZ = FalconY;

                            if (maxX - minX > IddleDelta || maxY - minY > IddleDelta || maxZ - minZ > IddleDelta || buttonPressed)
                            {
                                trimmingStart = false;
                            }
                            if(trimmingStart==false)
                                linesStack.Push(new DataSet(FalconX, FalconY, FalconZ, buttonPressed, line));
                        }
                        if (linesStack.Count > 0)   //if there's at least one line in the queue, use the last values as mins and maxs
                        {
                            minX = maxX = FalconX;
                            minY = maxY = FalconY;
                            minZ = maxZ = FalconZ;
                            do
                            {
                                DataSet dataSet = linesStack.Peek();
                                if (dataSet.x < minX)
                                    minX = dataSet.x;
                                if (dataSet.x > maxX)
                                    maxX = dataSet.x;
                                if (dataSet.y< minY)
                                    minY = dataSet.y;
                                if (dataSet.y> maxY)
                                    maxY = dataSet.y;
                                if (dataSet.z < minZ)
                                    minZ = dataSet.z;
                                if (dataSet.z > maxZ)
                                    maxZ = dataSet.y;

                                if (dataSet.buttonPressed || maxX - minX > IddleDelta || maxY - minY > IddleDelta || maxZ - minZ > IddleDelta)
                                {
                                    break;
                                }
                                linesStack.Pop();
                            } while (linesStack.Count > 0);
                        }
                        
                        Queue<DataSet> queue = new Queue<DataSet>(linesStack.Reverse());
                        while(queue.Count>0)
						{
							writer.WriteLine( queue.Dequeue().line );
						}
						writer.WriteLine("End of log");
					    writer.Close();
						reader.Close();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error trimming line \n" + line + "\n  " + ex);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error reading gamelog file "+ex);
                }
                Console.Write(".");
            }
            Console.WriteLine("done trimming.");
        }

        //Read the values recorded in the history log and display them in a text dialog.
        static void createReportPerGame(string username, string logsPath)
        {
            //Count time played per day
            string pattern = "GameLog_" + username + "_*t.csv";
            List<string> filePaths = new List<string>(Directory.GetFiles(logsPath+"/", pattern));

            Dictionary<string, Dictionary<string, int>> secondsPerGamePerDay = new Dictionary<string, Dictionary<string, int>>();
            Dictionary<string, Dictionary<string, double>> maxLiftPerGamePerDay = new Dictionary<string, Dictionary<string, double>>();
            Dictionary<string, Dictionary<string, double>> liftThresholdPerGamePerDay = new Dictionary<string, Dictionary<string, double>>();
            Dictionary<string, Dictionary<string, double>> liftRequiredPerGamePerDay = new Dictionary<string, Dictionary<string, double>>();
            Dictionary<string, Dictionary<string, double>> minWeightPerGamePerDay = new Dictionary<string, Dictionary<string, double>>();
            Dictionary<string, Dictionary<string, double>> maxWeightPerGamePerDay = new Dictionary<string, Dictionary<string, double>>();
            Dictionary<string, Dictionary<string, int>> totalValidLiftsPerGamePerDay = new Dictionary<string, Dictionary<string, int>>();
            Dictionary<string, Dictionary<string, int>> totalOverMaxLiftsPerGamePerDay = new Dictionary<string, Dictionary<string, int>>();

            Dictionary<string, Dictionary<string, double>> movementRequiredPerGamePerDay = new Dictionary<string, Dictionary<string, double>>();
            Dictionary<string, Dictionary<string, int>> totalValidMovementsPerGamePerDay = new Dictionary<string, Dictionary<string, int>>();

            Dictionary<string, Dictionary<string, int>> totalButtonPressesPerGamePerDay = new Dictionary<string, Dictionary<string, int>>();

            //Specific game metrics
            int secondsPlayingWristGames = 0, secondsPlayingElbowShoulderGames = 0;
            double millisecondsHoldingButtonDown = 0;
            int grandTotalWristExtensions = 0, grandTotalWristExtensionsOverMax = 0, grandTotalElbowShoulderMovements = 0, grandTotalButtonPresses = 0;
            double longestSustainedLift = 0;

            Console.WriteLine("Analyzing "+ logsPath+" ");
            foreach (string fileIn in filePaths)
            {
                bool liftValuesCalculated = false;
                double liftThreshold = -999;
                double liftRequired = -999;
                double requiredY = -999;
                double requiredYToStart = -999;
                double maximumY = -999;
                double maxWeight = -999;
                double minWeight = 999;
                bool belowLiftThreshold = true;
                bool countLiftOverMax = true;
                int totalValidLifts = 0;
                int totalLiftsOverMax = 0;

                bool movementValuesCalculated = false;
                double rightThreshold = -999;
                double leftThreshold = 999;
                double upThreshold = -999;
                double downThreshold = 999;
                double movementRequired = -999;
                double leftRequired = -999;
                double rightRequired = -999;
                double upRequired = -999;
                double downRequired = -999;
                bool countLeft = false;
                bool countRight = false;
                bool countUp = false;
                bool countDown = false;
                int totalValidMovements = 0;

                bool countButtonPress = true;
                int totalButtonPresses = 0;
                bool analyzingLiftingGame = false;
                
                try
                {
                    StreamReader reader = new StreamReader(fileIn);
                    String line = reader.ReadLine();
                    String[] data = line.Split(',');    //First line contains title 'Date & Time:' and the 'value'
                    data = data[1].Split('_');
                    string date = data[0];
                    line = reader.ReadLine();   //Game line
                    data = line.Split(',');
                    string game = data[1];

                    line = reader.ReadLine();   //skip username (already in file name)
                    line = reader.ReadLine();   //skip data titles

                    if (game == "WristAssessment")
                        game = "Wrist Assessment";
                    analyzingLiftingGame = isLiftingGame(game);
                    
                    bool counting = !analyzingLiftingGame; //start counting if type of game is not lifting. If lifting, counting will start later, when the first lift is detected

                    if (analyzingLiftingGame)
                    {
                        if (secondsPerGamePerDay.ContainsKey(game) == false)
                        {
                            secondsPerGamePerDay.Add(game, new Dictionary<string, int>());
                            maxLiftPerGamePerDay.Add(game, new Dictionary<string, double>());
                            liftThresholdPerGamePerDay.Add(game, new Dictionary<string, double>());
                            liftRequiredPerGamePerDay.Add(game, new Dictionary<string, double>());
                            minWeightPerGamePerDay.Add(game, new Dictionary<string, double>());
                            maxWeightPerGamePerDay.Add(game, new Dictionary<string, double>());
                            totalValidLiftsPerGamePerDay.Add(game, new Dictionary<string, int>());
                            totalOverMaxLiftsPerGamePerDay.Add(game, new Dictionary<string, int>());
                            totalButtonPressesPerGamePerDay.Add(game, new Dictionary<string, int>());
                        }
                        if (secondsPerGamePerDay[game].ContainsKey(date) == false)
                        {
                            secondsPerGamePerDay[game].Add(date, 0);
                            maxLiftPerGamePerDay[game].Add(date, -999);
                            liftThresholdPerGamePerDay[game].Add(date, -999);
                            liftRequiredPerGamePerDay[game].Add(date, -999);
                            minWeightPerGamePerDay[game].Add(date, 999);
                            maxWeightPerGamePerDay[game].Add(date, -999);
                            totalValidLiftsPerGamePerDay[game].Add(date, 0);
                            totalOverMaxLiftsPerGamePerDay[game].Add(date, 0);
                            totalButtonPressesPerGamePerDay[game].Add(date, 0);
                        }
                    }
                    else
                    {
                        if (secondsPerGamePerDay.ContainsKey(game) == false)
                        {
                            secondsPerGamePerDay.Add(game, new Dictionary<string, int>());
                            movementRequiredPerGamePerDay.Add(game, new Dictionary<string, double>());
                            minWeightPerGamePerDay.Add(game, new Dictionary<string, double>());
                            maxWeightPerGamePerDay.Add(game, new Dictionary<string, double>());
                            totalValidMovementsPerGamePerDay.Add(game, new Dictionary<string, int>());
                            totalButtonPressesPerGamePerDay.Add(game, new Dictionary<string, int>());
                        }
                        if (secondsPerGamePerDay[game].ContainsKey(date) == false)
                        {
                            secondsPerGamePerDay[game].Add(date, 0);
                            movementRequiredPerGamePerDay[game].Add(date, -999);
                            minWeightPerGamePerDay[game].Add(date, 999);
                            maxWeightPerGamePerDay[game].Add(date, -999);
                            totalValidMovementsPerGamePerDay[game].Add(date, 0);
                            totalButtonPressesPerGamePerDay[game].Add(date, 0);
                        }
                    }
                    
                    string lastSecondStamp = "";
                    string secondStamp;
                    string timeStamp;
                    int seconds = 0;
                    DateTime timeLiftStarted = DateTime.Now, timeLiftFinished = DateTime.Now;
                    DateTime timeButtonPressStarted = DateTime.Now, timeButtonPressFinished = DateTime.Now;
                    try
                    {
                        while ((line = reader.ReadLine()) != null)
                        {
                            data = line.Split(',');
                            if (data[0] == "End of log")
                                break;
                            timeStamp = data[0];
                            secondStamp = data[0].Substring(11, 8);    //extracts second
                            
                            float FalconX = float.Parse(data[1]);
                            float FalconY = float.Parse(data[2]);
                            bool buttonPressed = bool.Parse(data[14].ToLower());
                            
                            if (countButtonPress && buttonPressed)
                            {
                                totalButtonPresses++;
                                countButtonPress = false;
                                timeButtonPressStarted = getDateTime(timeStamp);
                            }
                            else
                                if (countButtonPress == false && buttonPressed == false)
                                {
                                    countButtonPress = true;
                                    timeButtonPressFinished = getDateTime(timeStamp);
                                    millisecondsHoldingButtonDown += timeButtonPressFinished.Subtract(timeButtonPressStarted).TotalMilliseconds;
                                }

                            double weight;
                            if (analyzingLiftingGame)
                            {
                                weight = float.Parse(data[7]);
                                if (weight < minWeight)
                                    minWeight = weight;
                                if (weight > maxWeight)
                                    maxWeight = weight;
                                
                                //counting peaks
                                if (liftValuesCalculated == false)
                                {
                                    liftThreshold = double.Parse(data[5]);
                                    liftRequired = double.Parse(data[6]);
                                    requiredY = FalconMinY + (liftThreshold - FalconMinY) * liftRequired;
                                    requiredYToStart = FalconMinY + (liftThreshold - FalconMinY) * LiftPercentToStart;
                                    liftValuesCalculated = true;
                                }
                                if (counting == false)    //only possible when lifting game
                                {
                                    if (FalconY >= requiredYToStart)
                                    {
                                        counting = true;
                                    }
                                }
                                if (counting)
                                {
                                    if (lastSecondStamp != secondStamp)
                                    {
                                        lastSecondStamp = secondStamp;
                                        seconds++;
                                        secondsPlayingWristGames++;
                                    }

                                    //over required
                                    if (FalconY >= requiredY && belowLiftThreshold)
                                    {
                                        belowLiftThreshold = false;
                                        totalValidLifts++;
                                        timeLiftStarted = getDateTime(timeStamp);
                                    }
                                    else
                                        if (belowLiftThreshold == false && FalconY < requiredY)  //when was counting lift and finished
                                        {
                                            belowLiftThreshold = true;
                                            timeLiftFinished = getDateTime(timeStamp);
                                            double timeLifted = timeLiftFinished.Subtract(timeLiftStarted).TotalMilliseconds;
                                            if (timeLifted > longestSustainedLift)
                                                longestSustainedLift = timeLifted;
                                        }
                                    //over measured max extension
                                    if (countLiftOverMax && FalconY >= liftThreshold)
                                    {
                                        totalLiftsOverMax++;
                                        countLiftOverMax = false;
                                    }
                                    else
                                        if (countLiftOverMax == false && FalconY < liftThreshold)
                                        {
                                            countLiftOverMax = true;
                                        }
                                    //Calculating maximum lift per game
                                    if (FalconY > maximumY)
                                    {
                                        maximumY = FalconY;
                                    }
                                }
                            }
                            else //analyzing elbow/shoulder game
                            {
                                if (lastSecondStamp != secondStamp)
                                {
                                    lastSecondStamp = secondStamp;
                                    seconds++;
                                    secondsPlayingElbowShoulderGames++;
                                }
                                weight = float.Parse(data[13]);
                                if (weight < minWeight)
                                    minWeight = weight;
                                if (weight > maxWeight)
                                    maxWeight = weight;
                                //counting peaks
                                if (movementValuesCalculated == false)
                                {
                                    upThreshold = double.Parse(data[8]);
                                    downThreshold = double.Parse(data[9]);
                                    leftThreshold = double.Parse(data[10]);
                                    rightThreshold = double.Parse(data[11]);
                                    movementRequired = double.Parse(data[12]);
                                    
                                    leftRequired = leftThreshold * movementRequired;
                                    rightRequired = rightThreshold * movementRequired;
                                    upRequired = upThreshold * movementRequired;
                                    downRequired = downThreshold * movementRequired;
                                    liftValuesCalculated = true;
                                }
                                //over required movement
                                if (countLeft && FalconX <= leftRequired)
                                {
                                    totalValidMovements++;
                                    countLeft = false;
                                }
                                else
                                    if (countLeft == false && FalconX > leftRequired)
                                    {
                                        countLeft = true;
                                    }
                                if (countRight && FalconX >= rightRequired)
                                {
                                    totalValidMovements++;
                                    countRight = false;
                                }
                                else
                                    if (countRight == false && FalconX < rightRequired)
                                    {
                                        countRight = true;
                                    }

                                if (countDown && FalconY <= downRequired)
                                {
                                    totalValidMovements++;
                                    countDown = false;
                                }
                                else
                                    if (countDown == false && FalconY > downRequired)
                                    {
                                        countDown = true;
                                    }
                                if (countUp && FalconY >= upRequired)
                                {
                                    totalValidMovements++;
                                    countUp = false;
                                }
                                else
                                    if (countUp == false && FalconY < upRequired)
                                    {
                                        countUp = true;
                                    }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error in line \n" + line + "\n  " + ex);
                    }

                    secondsPerGamePerDay[game][date] += seconds;
                    if (maxWeight > maxWeightPerGamePerDay[game][date])
                    {
                        maxWeightPerGamePerDay[game][date] = maxWeight;
                    }
                    if (minWeight < minWeightPerGamePerDay[game][date])
                    {
                        minWeightPerGamePerDay[game][date] = minWeight;
                    }
                    totalButtonPressesPerGamePerDay[game][date] += totalButtonPresses;
                    if (analyzingLiftingGame)
                    {
                        totalValidLiftsPerGamePerDay[game][date] += totalValidLifts;
                        totalOverMaxLiftsPerGamePerDay[game][date] += totalLiftsOverMax;
                        if (maximumY > maxLiftPerGamePerDay[game][date])
                        {
                            maxLiftPerGamePerDay[game][date] = maximumY;
                        }
                        liftThresholdPerGamePerDay[game][date] = liftThreshold;
                        liftRequiredPerGamePerDay[game][date] = liftRequired;
                    }
                    else //analyzing elbow/shoulder game
                    {
                        totalValidMovementsPerGamePerDay[game][date] += totalValidMovements;
                        movementRequiredPerGamePerDay[game][date] = movementRequired;
                    }

                    reader.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error reading gamelog file");
                }
                Console.Write(".");
            }

            StreamWriter writer = new StreamWriter(logsPath + username + " AnalysisPerGame.csv");
            Console.Write("\nCreating analysis file.");
            foreach (KeyValuePair<string, Dictionary<string, int>> secondsPerGame in secondsPerGamePerDay)
            {
                string game = secondsPerGame.Key;
                
                writer.WriteLine(game + ":");  //game name
                if (isLiftingGame(game))
                {
                    writer.WriteLine(",Date,Seconds,TotalValidLifts,TotalOverMaxLifts,MaxLift,MaxLift(%),MeasuredMaxLift,PercentOfMaxLiftRequired,MaxWeight,MinWeight,TotalButtonPresses");
                    
                    foreach (KeyValuePair<string, int> secondsPerDay in secondsPerGame.Value)
                    {
                        string date = secondsPerDay.Key;
                        writer.WriteLine("," + date + "," +
                            secondsPerDay.Value + "," +
                            totalValidLiftsPerGamePerDay[game][date] + "," +
                            totalOverMaxLiftsPerGamePerDay[game][date] + "," +
                            maxLiftPerGamePerDay[game][date] + "," +
                            (maxLiftPerGamePerDay[game][date] - FalconMinY) / (liftThresholdPerGamePerDay[game][date] - FalconMinY) + "," +
                            liftThresholdPerGamePerDay[game][date] + "," +
                            liftRequiredPerGamePerDay[game][date] + "," +
                            maxWeightPerGamePerDay[game][date] + "," +
                            minWeightPerGamePerDay[game][date] + "," +
                            totalButtonPressesPerGamePerDay[game][date]);
                    }
                    writer.WriteLine(game + ",Total:," + secondsPerGame.Value.Values.Sum() + "," + totalValidLiftsPerGamePerDay[game].Values.Sum() + "," +
                        totalOverMaxLiftsPerGamePerDay[game].Values.Sum() + "," + maxLiftPerGamePerDay[game].Values.Max() + "," +
                        "" + "," + liftThresholdPerGamePerDay[game].Values.Max() +"," + liftRequiredPerGamePerDay[game].Values.Max() + "," +
                        maxWeightPerGamePerDay[game].Values.Max() + "," + minWeightPerGamePerDay[game].Values.Min() + "," +
                        totalButtonPressesPerGamePerDay[game].Values.Sum()
                        + "\n" );
                    grandTotalWristExtensions += totalValidLiftsPerGamePerDay[game].Values.Sum();
                    grandTotalWristExtensionsOverMax += totalOverMaxLiftsPerGamePerDay[game].Values.Sum();
                }
                else
                {
                    writer.WriteLine(",Date,Seconds,TotalValidMovements,PercentOfMovementRequired,MaxResistance,MinResistance,TotalButtonPresses");
                    foreach (KeyValuePair<string, int> secondsPerDay in secondsPerGame.Value)
                    {
                        string date = secondsPerDay.Key;
                        writer.WriteLine("," + date + "," +
                            secondsPerDay.Value + "," +
                            totalValidMovementsPerGamePerDay[game][date] + "," +
                            movementRequiredPerGamePerDay[game][date] + "," +
                            maxWeightPerGamePerDay[game][date] + "," +
                            minWeightPerGamePerDay[game][date] + "," +
                            totalButtonPressesPerGamePerDay[game][date]);
                    }
                    writer.WriteLine(game+",Total:," + secondsPerGame.Value.Values.Sum() + "," + totalValidMovementsPerGamePerDay[game].Values.Sum() + "," +
                        movementRequiredPerGamePerDay[game].Values.Max() + "," +
                        maxWeightPerGamePerDay[game].Values.Max() + "," + minWeightPerGamePerDay[game].Values.Min() + "," +
                        totalButtonPressesPerGamePerDay[game].Values.Sum()
                        + "\n");
                    grandTotalElbowShoulderMovements += totalValidMovementsPerGamePerDay[game].Values.Sum();
                }
                grandTotalButtonPresses += totalButtonPressesPerGamePerDay[game].Values.Sum();
                
                Console.Write(".");
            }
            writer.WriteLine("\nGame metrics");
            writer.WriteLine("Grand total minutes playing wrist games," + secondsPlayingWristGames/60f);
            writer.WriteLine("Grand total minutes playing elbow/soulder games," + secondsPlayingElbowShoulderGames/ 60f);
            writer.WriteLine("Grand total wrist extensions," + grandTotalWristExtensions);
            writer.WriteLine("Grand total wrist extensions over measured max," + grandTotalWristExtensionsOverMax);
            writer.WriteLine("Grand total elbow/shoulder movements," + grandTotalElbowShoulderMovements);
            writer.WriteLine("Grand total button presses," + grandTotalButtonPresses);
            writer.WriteLine("Longest sustained lift (seconds)," + longestSustainedLift/1000);
            writer.WriteLine("Grand total time holding button down (minutes)," + millisecondsHoldingButtonDown/60000);
            writer.Close();
            Console.WriteLine("\ndone analyzing.");
        }

        private static DateTime getDateTime(string timeStamp)
        {
            return DateTime.ParseExact(timeStamp, "yyyy-MM-dd_HH-mm-ss-fff", null);
        }

        //-------------------------------------------------------------------------------------------------------------------------
        //Read the values recorded in the history log and display them in a text dialog.
        static void createReportBasedOnDate(string username, string logsPath)
        {
            //Count time played per day
            string pattern = "GameLog_" + username + "_*t.csv";
            List<string> filePaths = new List<string>(Directory.GetFiles(logsPath + "/", pattern));

            Console.WriteLine("Analyzing based on date " + logsPath);
            StreamWriter writer = new StreamWriter(logsPath + username+" AnalysisPerDate.csv");
            writer.WriteLine("Date,Game,Seconds,TotalValidMovements,PercentOfMovementRequired,MaxResistance,MinResistance,TotalButtonPresses,TotalOverMaxLifts,MaxLift,MaxLift(%),MeasuredMaxLift");
            
            string previousDay = "";
                
            foreach (string fileIn in filePaths)
            {
                //Remove data before first wrist extension over 50%
                bool counting = false;
                bool analyzingLiftingGame = false;
                bool liftValuesCalculated = false;
                bool movementValuesCalculated = false;
                
                double liftThreshold = -999;
                double liftRequired = -999;
                double requiredY = -999;
                double liftPercentToStart = 0.3;
                double requiredYToStart = -999;
                double maxY = -999;
                double maxWeight = -999;
                double minWeight = 999;
                bool countLift = true;
                bool countLiftOverMax = true;
                int totalValidLifts = 0;
                int totalLiftsOverMax = 0;

                double rightThreshold = -999;
                double leftThreshold = 999;
                double upThreshold = -999;
                double downThreshold = 999;
                double movementRequired = -999;
                double leftRequired = -999;
                double rightRequired = -999;
                double upRequired = -999;
                double downRequired = -999;
                bool countLeft = false;
                bool countRight = false;
                bool countUp = false;
                bool countDown = false;
                int totalValidMovements = 0;

                bool countButtonPress = true;
                int totalButtonPresses = 0;
                try
                {
                    StreamReader reader = new StreamReader(fileIn);
                    String line = reader.ReadLine();
                    String[] data = line.Split(',');    //First line contains title 'Date & Time:' and the date and time 'value'
                    data = data[1].Split('_');
                    string date = data[0];
                    if(date != previousDay)
                    {
                        if (previousDay != "")
                            writer.WriteLine();
                        previousDay = date;
                    }
                    string time = data[1];
                    date = date + '_' + time;

                    line = reader.ReadLine();   //Game line
                    data = line.Split(',');
                    string game = data[1];
                    if (game == "WristAssessment")
                        game = "Wrist Assessment";
                    analyzingLiftingGame = isLiftingGame(game);
                    
                    line = reader.ReadLine();   //skip username (already in file name)
                    line = reader.ReadLine();   //skip data titles

                    if (analyzingLiftingGame == false)
                    {
                        counting = true;
                    }

                    string lastSecondStamp = "";
                    string secondStamp;
                    int seconds = 0;
                    float FalconX;
                    float FalconY;
                    double weight;
                    bool buttonPressed;

                    try
                    {
                        while ((line = reader.ReadLine()) != null)
                        {
                            data = line.Split(',');
                            if (data.Count() < 15 || data[0] == "End of log")
                                break;
                            secondStamp = data[0].Substring(11, 8);    //extracts second
                            FalconX = float.Parse(data[1]);
                            FalconY = float.Parse(data[2]);
                            buttonPressed = bool.Parse(data[14].ToLower());

                            if (analyzingLiftingGame)
                            {
                                if (liftValuesCalculated == false)
                                {
                                    liftThreshold = double.Parse(data[5]);
                                    liftRequired = double.Parse(data[6]);
                                    requiredY = FalconMinY + (liftThreshold - FalconMinY) * liftRequired;
                                    requiredYToStart = FalconMinY + (liftThreshold - FalconMinY) * liftPercentToStart;
                                    liftValuesCalculated = true;
                                }
                                weight = float.Parse(data[7]);
                                if (weight < minWeight)
                                    minWeight = weight;
                                if (weight > maxWeight)
                                    maxWeight = weight;
                            }
                            else
                            {
                                if (movementValuesCalculated == false)
                                {
                                    upThreshold = double.Parse(data[8]);
                                    downThreshold = double.Parse(data[9]);
                                    leftThreshold = double.Parse(data[10]);
                                    rightThreshold = double.Parse(data[11]);
                                    movementRequired = double.Parse(data[12]);

                                    leftRequired = leftThreshold * movementRequired;
                                    rightRequired = rightThreshold * movementRequired;
                                    upRequired = upThreshold * movementRequired;
                                    downRequired = downThreshold * movementRequired;
                                    liftValuesCalculated = true;
                                }
                                weight = float.Parse(data[13]);
                                if (weight < minWeight)
                                    minWeight = weight;
                                if (weight > maxWeight)
                                    maxWeight = weight;
                            }

                            if (counting==false)    //only possible when lifting game
                            {   
                                if (FalconY >= requiredYToStart )
                                {
                                    counting = true;
                                }
                            }
                            if(counting)
                            {
                                if (lastSecondStamp != secondStamp)
                                {
                                    lastSecondStamp = secondStamp;
                                    seconds++;
                                }
                                if (countButtonPress && buttonPressed)
                                {
                                    totalButtonPresses++;
                                    countButtonPress = false;
                                }
                                else
                                    if (countButtonPress == false && buttonPressed == false)
                                    {
                                        countButtonPress = true;
                                    }
                                if (analyzingLiftingGame)
                                {
                                    //over required
                                    if (countLift && FalconY >= requiredY)
                                    {
                                        totalValidLifts++;
                                        countLift = false;
                                    }
                                    else
                                        if (countLift == false && FalconY < requiredY)
                                        {
                                            countLift = true;
                                        }
                                    //over measured max extension
                                    if (countLiftOverMax && FalconY >= liftThreshold)
                                    {
                                        totalLiftsOverMax++;
                                        countLiftOverMax = false;
                                    }
                                    else
                                        if (countLiftOverMax == false && FalconY < liftThreshold)
                                        {
                                            countLiftOverMax = true;
                                        }
                                    //Calculating maximum lift per game
                                    if (FalconY > maxY)
                                    {
                                        maxY = FalconY;
                                    }
                                }
                                else //analyzing elbow/shoulder game
                                {
                                    //over required movement
                                    if (countLeft && FalconX <= leftRequired)
                                    {
                                        totalValidMovements++;
                                        countLeft = false;
                                    }
                                    else
                                        if (countLeft == false && FalconX > leftRequired)
                                        {
                                            countLeft = true;
                                        }
                                    if (countRight && FalconX >= rightRequired)
                                    {
                                        totalValidMovements++;
                                        countRight = false;
                                    }
                                    else
                                        if (countRight == false && FalconX < rightRequired)
                                        {
                                            countRight = true;
                                        }

                                    if (countDown && FalconY <= downRequired)
                                    {
                                        totalValidMovements++;
                                        countDown = false;
                                    }
                                    else
                                        if (countDown == false && FalconY > downRequired)
                                        {
                                            countDown = true;
                                        }
                                    if (countUp && FalconY >= upRequired)
                                    {
                                        totalValidMovements++;
                                        countUp = false;
                                    }
                                    else
                                        if (countUp == false && FalconY < upRequired)
                                        {
                                            countUp = true;
                                        }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error in line \n" + line + "\n  " + ex);
                    }

                    if (analyzingLiftingGame)
                    {
                        writer.WriteLine(
                            date + "," +
                            game + "," + 
                            seconds + "," +
                            totalValidLifts + "," +
                            liftRequired + "," +
                            maxWeight + "," +
                            minWeight + "," +
                            totalButtonPresses + "," +
                            totalLiftsOverMax + "," +
                            maxY + "," +
                            (maxY - FalconMinY) / (liftThreshold - FalconMinY) + "," +
                            liftThreshold);
                    }
                    else
                    {
                        writer.WriteLine(
                           date + "," +
                           game + "," +
                           seconds + "," +
                           totalValidMovements + "," +
                           movementRequired + "," +
                           maxWeight + "," +
                           minWeight + "," +
                           totalButtonPresses);
                    }
                    reader.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error reading gamelog file: "+ex.Message);
                }
                Console.Write(".");
            }
            writer.Close();
            Console.WriteLine("\ndone analyzing.");
        }

        private static bool isLiftingGame(string game)
        {
            switch (game)
            {
                case "WristAssessment":
                case "Wrist Assessment":
                case "SustainedWristAssessment":
                case "Funky Karts":
                case "Crazy Rider":
                case "Skater Boy":
                case "BMX Boy":
                case "Lil Mads and the Gold Skull":
                case "Swoop":
                case "Swooop":
                case "Botley's Bootles":
                case "Botley's Bootles Coins":
                    return true;
                default: return false;
            }
        }

        static void fixLogFilesV1(string username, string logsPath)
        {
            //Count time played per day
            string pattern = "GameLog_" + username + "_*.csv";
            List<string> filePaths = new List<string>(Directory.GetFiles(logsPath + "/", pattern));
            int count = 0;
            Console.Write("Fixing " + logsPath + " ");
            foreach (string fileIn in filePaths)
            {
                try
                {
                    string file = fileIn.Substring(0, fileIn.Length - 1);
                    File.Move(fileIn, file);
                    
                    StreamWriter writer = new StreamWriter(fileIn);
                    StreamReader reader = new StreamReader(file);
                    String line = reader.ReadLine();    //date
                    writer.WriteLine(line);
                    line = reader.ReadLine();    //Game
                    writer.WriteLine(line);
                    line = reader.ReadLine();   //username (already in file name)
                    writer.WriteLine(line);
                    line = reader.ReadLine();   //Titles
                    //First line to fix
                    writer.WriteLine("Time,FalconX,FalconY,FalconZ,YRequired,LiftThreshold,LiftRequired,FalconWeight,UpThreshold,DownThreshold,LeftThreshold,RightThreshold,MovementRequired,FalconResistance,RedButtonPressed");
                    string[] data;
                    try
                    {
                        while ((line = reader.ReadLine()) != null)
                        {
                            data = line.Split(',');
                            if (data[0] == "End of log")
                            {
                                break;
                            }
                            if (data.Count() > 1)
                            {
                                if (data.Count() < 15)
                                {
                                    string comma = "";
                                    for (int i = 0; i < data.Count(); i++)
                                    {
                                        if (i == 4)
                                            writer.Write(comma + (FalconMinY+( float.Parse(data[4]) -FalconMinY)*float.Parse(data[5])));
                                        writer.Write(comma + data[i]);
                                        comma = ",";
                                    }
                                    writer.WriteLine();
                                }
                            }
                        }
                        writer.WriteLine("End of log");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error in line \n" + line + "\n  " + ex);
                    }
                    reader.Close();
                    writer.Close();
                    count++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error reading gamelog file");
                }
                Console.Write(".");
            }
            Console.WriteLine("Fixed "+count+" files.");
        }
        static void fixLogFilesV2(string username, string logsPath)
        {
            //Count time played per day
            string pattern = "GameLog_" + username + "_*.csv";
            List<string> filePaths = new List<string>(Directory.GetFiles(logsPath + "/", pattern));


            Console.Write("Fixing " + logsPath + " ");
            foreach (string fileIn in filePaths)
            {
                try
                {
                    string file = fileIn.Substring(0,fileIn.Length-1);
                    File.Move(fileIn, file);
                    StreamReader reader = new StreamReader(file);
                    StreamWriter writer = new StreamWriter(fileIn);
                    
                    String line = reader.ReadLine();
                    writer.WriteLine(line);
                    line = reader.ReadLine();   //game name
                    writer.WriteLine(line);
                    line = reader.ReadLine();   //username
                    writer.WriteLine(line);
                    line = reader.ReadLine();   //Titles
                    //First line to fix
                    writer.WriteLine("Time,FalconX,FalconY,FalconZ,YRequired,LiftThreshold,LiftRequired,FalconWeight,UpThreshold,DownThreshold,LeftThreshold,RightThreshold,MovementRequired,FalconResistance,RedButtonPressed");
                    string[] data;
                    try
                    {
                        while ((line = reader.ReadLine()) != null)
                        {
                            data = line.Split(',');
                            if (data.Count()<14 || data[0] == "End of log")
                            {
                                writer.WriteLine("End of log");
                                break;
                            }
                            if (data.Count() > 1)
                            {
                                if (data[4] == "")
                                {
                                    string comma = "";
                                    for (int i = 0; i < data.Count(); i++)
                                    {
                                        if (i == 4)
                                        {
                                            i++;
                                            string[] data4slipt = data[5].Split('.');
                                            if (data4slipt[1].Contains('-'))
                                                writer.Write(comma + data4slipt[0] + "." + data4slipt[1].Substring(0, data4slipt.Length - 2) + "," +
                                                    data4slipt[1].Substring(data4slipt.Length - 2) + "." + data4slipt[2]);
                                            else
                                                writer.Write(comma + data4slipt[0] + "." + data4slipt[1].Substring(0, data4slipt[1].Length - 1) + "," +
                                                    data4slipt[1].Substring(data4slipt[1].Length - 1) + "." + data4slipt[2]);
                                        }
                                        else
                                        {
                                            writer.Write(comma + data[i]);
                                        }
                                        comma = ",";
                                    }
                                    writer.WriteLine();
                                }
                            }
                            else
                            {
                                writer.WriteLine("End of log");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error in line \n" + line + "\n  " + ex);
                    }
                    reader.Close();
                    writer.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error reading gamelog file");
                }
                Console.Write(".");
            }
        }
        //This method fixes the millisecond stamp to use 3 digits always.
        static void fixLogFilesV3(string username, string logsPath)
        {
            Console.WriteLine("\nFixing logs to v3 (milliseconds timestamp): "+logsPath);
            string filenamesPattern = "GameLog_" + username + "_2015-12-08_*.csv";
            List<string> filePaths = new List<string>(Directory.GetFiles(logsPath + "/", filenamesPattern));
            foreach (string fileIn in filePaths)
            {
                string line="";
                try
                {
                    string file = fileIn.Substring(0, fileIn.Length - 4);
                    File.Move(fileIn, file+"v2.csv");
                    StreamReader reader = new StreamReader(file+"v2.csv");
                    StreamWriter writer = new StreamWriter(fileIn);
                    
                    writer.WriteLine(reader.ReadLine());    //Date and time
                    writer.WriteLine(reader.ReadLine());    //Game name
                    writer.WriteLine(reader.ReadLine());    //Username
                    writer.WriteLine(reader.ReadLine());    //Columns' titles
                    string[] data;
                    while ((line = reader.ReadLine()) != null)
                    {
                        data = line.Split(',');
                        if (data.Count() < 14 || data[0] == "End of log")
                        {
                            writer.WriteLine("End of log");
                            break;
                        }
                        string milliSeconds = data[0].Substring(20);
                        if (milliSeconds.Length == 2) milliSeconds = "0" + milliSeconds;
                        else
                            if (milliSeconds.Length == 1) milliSeconds = "00" + milliSeconds;
                        writer.Write(data[0].Substring(0,20)+milliSeconds);
                            
                        for (int i = 1; i < data.Count(); i++)
                        {
                            writer.Write("," + data[i]);
                        }
                        writer.WriteLine();
                    }
                    
                    reader.Close();
                    writer.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error analyzing file "+fileIn+" in line \n" + line + "\n  " + ex);
                }
                Console.Write(".");
            }
            Console.WriteLine("Done converting log files to v3");
        }
    }
}
