using DotNetTwitchBot.Bot.Markov.Components;
using DotNetTwitchBot.Bot.Markov.Models;
using DotNetTwitchBot.Bot.Markov.TokenisationStrategies;
using Serilog;

namespace DotNetTwitchBot.Bot.Markov
{
    /// <summary>
    /// This class contains core functionality of the generic Markov model.
    /// Shouldn't be used directly, instead, extend GenericMarkov 
    /// and implement the IMarkovModel interface - this will allow you to 
    /// define overrides for SplitTokens and RebuildPhrase, which is generally
    /// all that should be needed for implementation of a new model type.
    /// </summary>
    /// <typeparam name="TPhrase">The type representing the entire phrase</typeparam>
    /// <typeparam name="TUnigram">The type representing a unigram, or state</typeparam>
    public abstract class GenericMarkov(ILogger<GenericMarkov> logger) : IMarkovStrategy
    {

        // Chain containing the model data. The key is the N number of
        // previous words and value is a list of possible outcomes, given that key
        public MarkovChain Chain { get; set; } = new MarkovChain();

        /// <summary>
        /// Used for defining a strategy to select the next value when calling Walk()
        /// Set this to something else for different behaviours
        /// </summary>
        public IUnigramSelector UnigramSelector { get; set; } = new MostPopularRandomUnigramSelector();

        public List<string> SourcePhrases { get; set; } = new List<string>();

        /// <summary>
        /// Defines how to split the phrase to ngrams
        /// </summary>
        /// <param name="phrase"></param>
        /// <returns></returns>
        public abstract IEnumerable<string> SplitTokens(string phrase);

        /// <summary>
        /// Defines how to join ngrams back together to form a phrase
        /// </summary>
        /// <param name="tokens"></param>
        /// <returns></returns>
        public abstract string RebuildPhrase(IEnumerable<string> tokens);

        public abstract string? GetTerminatorUnigram();

        public abstract string GetPrepadUnigram();

        /// <summary>
        /// Set to true to ensure that all lines generated are different and not same as the training data.
        /// This might not return as many lines as requested if genreation is exhausted and finds no new unique values.
        /// </summary>
        public bool EnsureUniqueWalk { get; set; } = false;

        // The number of previous states for the model to to consider when 
        //suggesting the next state
        public int Level { get; set; } = 2;

        public void Learn(IEnumerable<string> phrases, bool ignoreAlreadyLearnt = true)
        {
            if (ignoreAlreadyLearnt)
            {
                var newTerms = phrases.Where(s => !SourcePhrases.Contains(s));

                logger.LogInformation("Learning {count} lines", newTerms.Count());
                // For every sentence which hasnt already been learnt, learn it
                Parallel.ForEach(phrases, Learn);
            }
            else
            {
                logger.LogInformation("Learning {count} lines", phrases.Count());
                // For every sentence, learn it
                Parallel.ForEach(phrases, Learn);
            }
        }

        public void Learn(string phrase)
        {
            logger.LogDebug($"Learning phrase: '{phrase}'");
            if (phrase == null || phrase.Equals(default(string)))
            {
                return;
            }

            // Ignore particularly short phrases
            if (SplitTokens(phrase).Count() < Level)
            {
                logger.LogDebug($"Phrase {phrase} too short - skipped");
                return;
            }

            // Add it to the source lines so we can ignore it 
            // when learning in future
            if (!SourcePhrases.Contains(phrase))
            {
                logger.LogDebug($"Adding phrase {phrase} to source lines");
                SourcePhrases.Add(phrase);
            }

            // Split the sentence to an array of words
            var tokens = SplitTokens(phrase).ToArray();

            LearnTokens(tokens);

            var lastCol = new List<string>();
            for (var j = Level; j > 0; j--)
            {
                string previous;
                try
                {
                    previous = tokens[tokens.Length - j];
                    logger.LogDebug($"Adding TGram ({typeof(string)}) {previous} to lastCol");
                    lastCol.Add(previous);
                }
                catch (IndexOutOfRangeException e)
                {
                    logger.LogWarning($"Caught an exception: {e}");
                    previous = GetPrepadUnigram();
                    lastCol.Add(previous);
                }
            }

            logger.LogDebug($"Reached final key for phrase {phrase}");
            var finalKey = new NgramContainer<string>(lastCol.ToArray());
            Chain.AddOrCreate(finalKey, GetTerminatorUnigram());
        }

        /// <summary>
        /// Iterate over a list of TGrams and store each of them in the model at a composite key genreated from its prior [Level] number of TGrams
        /// </summary>
        /// <param name="tokens"></param>
        private void LearnTokens(IReadOnlyList<string> tokens)
        {
            for (var i = 0; i < tokens.Count; i++)
            {
                var current = tokens[i];
                var previousCol = new List<string>();

                // From the current token's index, get hold of the previous [Level] number of tokens that came before it
                for (var j = Level; j > 0; j--)
                {
                    string previous;
                    try
                    {
                        // this case addresses when we are at a token index less then the value of [Level], 
                        // and we effectively would be looking at tokens before the beginning phrase
                        if (i - j < 0)
                        {
                            previousCol.Add(GetPrepadUnigram());
                        }
                        else
                        {
                            previous = tokens[i - j];
                            previousCol.Add(previous);
                        }
                    }
                    catch (IndexOutOfRangeException)
                    {
                        previous = GetPrepadUnigram();
                        previousCol.Add(previous);
                    }
                }

                // create the composite key based on previous tokens
                var key = new NgramContainer<string>(previousCol.ToArray());

                // add the current token to the markov model at the composite key
                Chain.AddOrCreate(key, current);
            }
        }

        /// <summary>
        /// Retrain an existing trained model instance to a different 'level'
        /// </summary>
        /// <param name="newLevel"></param>
        public void Retrain(int newLevel)
        {
            if (newLevel < 1)
            {
                throw new ArgumentException("Invalid argument - retrain level must be a positive integer", nameof(newLevel));
            }

            logger.LogInformation($"Retraining model as level {newLevel}");
            Level = newLevel;

            // Empty the model so it can be rebuilt
            Chain = new MarkovChain();

            Learn(SourcePhrases, false);
        }



        /// <summary>
        /// Generate a collection of phrase output data based on the current model
        /// </summary>
        /// <param name="lines">The number of phrases to emit</param>
        /// <param name="seed">Optionally provide the start of the phrase to generate from</param>
        /// <returns></returns>
        public IEnumerable<string> Walk(int lines = 1, string? seed = default)
        {
            if (seed == null)
            {
                seed = RebuildPhrase(new List<string>() { GetPrepadUnigram() });
            }

            logger.LogDebug($"Walking to return {lines} phrases from {Chain.Count} states");
            if (lines < 1)
            {
                throw new ArgumentException("Invalid argument - line count for walk must be a positive integer", nameof(lines));
            }

            var sentences = new List<string>();

            int genCount = 0;
            int created = 0;
            while (created < lines)
            {
                if (genCount == lines * 10)
                {
                    logger.LogInformation($"Breaking out of walk early - {genCount} generations did not produce {lines} distinct lines ({sentences.Count} were created)");
                    break;
                }
                var result = WalkLine(seed);
                if ((!EnsureUniqueWalk || !SourcePhrases.Contains(result)) && (!EnsureUniqueWalk || !sentences.Contains(result)))
                {
                    sentences.Add(result);
                    created++;
                    yield return result;
                }
                genCount++;
            }
        }

        /// <summary>
        /// Generate a single phrase of output data based on the current model
        /// </summary>
        /// <param name="seed">Optionally provide the start of the phrase to generate from</param>
        /// <returns></returns>
        private string WalkLine(string seed)
        {
#pragma warning disable CS8604 // Possible null reference argument.
            var paddedSeed = PadArrayLow(SplitTokens(seed)?.ToArray());
#pragma warning restore CS8604 // Possible null reference argument.
            var built = new List<string>();

            // Allocate a queue to act as the memory, which is n 
            // levels deep of previous words that were used
            var q = new Queue<string>(paddedSeed);

            // If the start of the generated text has been seeded,
            // append that before generating the rest
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            if (!seed.Equals(GetPrepadUnigram()))
            {
                built.AddRange(SplitTokens(seed));
            }
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            while (built.Count < 25)
            {
                // Choose a new token to add from the model
                var key = new NgramContainer<string>(q.Cast<string>().ToArray());
                if (Chain.Contains(key))
                {
                    string chosen;

                    if (built.Count == 0)
                    {
                        chosen = new MostPopulatorUnigramStarterSelector().SelectUnigram(Chain.GetValuesForKey(key));
                    }
                    else
                    {
                        chosen = UnigramSelector.SelectUnigram(Chain.GetValuesForKey(key));
                    }

                    q.Dequeue();
                    q.Enqueue(chosen);
                    built.Add(chosen);
                }
                else
                {
                    break;
                }
            }

            return RebuildPhrase(built);
        }

        // Pad out an array with empty strings from bottom up
        // Used when providing a seed sentence or word for generation
        private string[] PadArrayLow(string[] input)
        {
            if (input == null)
            {
                input = new List<string>().ToArray();
            }

            var splitCount = input.Length;
            if (splitCount > Level)
            {
                input = input.Skip(splitCount - Level).Take(Level).ToArray();
            }

            var p = new string[Level];
            var j = 0;
            for (var i = (Level - input.Length); i < (Level); i++)
            {
                p[i] = input[j];
                j++;
            }
            for (var i = Level - input.Length; i > 0; i--)
            {
                p[i - 1] = GetPrepadUnigram();
            }

            return p;
        }
    }
}
