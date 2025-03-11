using DotNetTwitchBot.Bot.Markov.Components;
using DotNetTwitchBot.Bot.Markov.Models;
using DotNetTwitchBot.Bot.Markov.TokenisationStrategies;
using Quartz.Util;
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
    public abstract class GenericMarkov(ILogger<GenericMarkov> logger, IServiceScopeFactory scopeFactory) : IMarkovStrategy
    {

        // Chain containing the model data. The key is the N number of
        // previous words and value is a list of possible outcomes, given that key
        public MarkovChain Chain { get; set; } = new MarkovChain(scopeFactory);

        /// <summary>
        /// Used for defining a strategy to select the next value when calling Walk()
        /// Set this to something else for different behaviours
        /// </summary>
        public IUnigramSelector UnigramSelector { get; set; } = new WeightedListUnigramSelector();

        //public List<string> SourcePhrases { get; set; } = new List<string>();

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

        // The number of previous states for the model to to consider when 
        //suggesting the next state
        public int Level { get; set; } = 2;

        public async Task Learn(IEnumerable<string> phrases)
        {

            if (phrases.Count() > 1)
            {
                logger.LogInformation("Learning {count} lines", phrases.Count());
            }
            // For every sentence, learn it
            await Parallel.ForEachAsync(phrases,async (phrase, ct) =>
            {
                await Learn(phrase);
            });

        }

        public async Task Learn(string phrase)
        {
            logger.LogDebug("Learning phrase: '{phrase}'", phrase);
            if (phrase == null || phrase.Equals(default))
            {
                return;
            }

            // Ignore particularly short phrases
            if (SplitTokens(phrase).Count() < Level)
            {
                logger.LogDebug("Phrase {phrase} too short - skipped", phrase);
                return;
            }

            // Split the sentence to an array of words
            var tokens = SplitTokens(phrase).ToArray();

            await LearnTokens(tokens);

            var lastCol = new List<string>();
            for (var j = Level; j > 0; j--)
            {
                string previous;
                try
                {
                    previous = tokens[^j];
                    logger.LogDebug("Adding TGram {previous} to lastCol", previous);
                    lastCol.Add(previous);
                }
                catch (IndexOutOfRangeException e)
                {
                    logger.LogWarning(e, "Caught an exception");
                    previous = "";
                    lastCol.Add(previous);
                }
            }

            logger.LogDebug("Reached final key for phrase {phrase}", phrase);
            var finalKey = new NgramContainer([.. lastCol]);
            await Chain.AddOrCreate(finalKey, null);
        }

        /// <summary>
        /// Iterate over a list of TGrams and store each of them in the model at a composite key genreated from its prior [Level] number of TGrams
        /// </summary>
        /// <param name="tokens"></param>
        private async Task LearnTokens(string[] tokens)
        {
            for (var i = 0; i < tokens.Length; i++)
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
                            previousCol.Add("");
                        }
                        else
                        {
                            previous = tokens[i - j];
                            previousCol.Add(previous);
                        }
                    }
                    catch (IndexOutOfRangeException)
                    {
                        previous = "";
                        previousCol.Add(previous);
                    }
                }

                // create the composite key based on previous tokens
                var key = new NgramContainer(previousCol.ToArray());

                // add the current token to the markov model at the composite key
                await Chain.AddOrCreate(key, current);
            }
        }

        /// <summary>
        /// Generate a collection of phrase output data based on the current model
        /// </summary>
        /// <param name="seed">Optionally provide the start of the phrase to generate from</param>
        /// <returns></returns>
        public async Task<string> Walk(string? seed = default)
        {
            if (seed == null)
            {
                seed = RebuildPhrase([""]);
            }
            return await WalkLine(seed);
        }

        /// <summary>
        /// Generate a single phrase of output data based on the current model
        /// </summary>
        /// <param name="seed">Optionally provide the start of the phrase to generate from</param>
        /// <returns></returns>
        private async Task<string> WalkLine(string seed)
        {

            var paddedSeed = PadArrayLow(SplitTokens(seed)?.ToArray());

            var built = new List<string>();

            // Allocate a queue to act as the memory, which is n 
            // levels deep of previous words that were used
            var q = new Queue<string>(paddedSeed);

            // If the start of the generated text has been seeded,
            // append that before generating the rest
            if (!seed.IsNullOrWhiteSpace())
            {
                built.AddRange(SplitTokens(seed));
            }

            while (built.Count < 25)
            {
                // Choose a new token to add from the model
                var key = new NgramContainer(q.Cast<string>().ToArray());
                if (await Chain.Contains(key))
                {
                    string chosen;

                    if (built.Count == 0)
                    {
                        chosen = new MostPopulatorUnigramStarterSelector().SelectUnigram(await Chain.GetValuesForKey(key));
                    }
                    else
                    {
                        chosen = UnigramSelector.SelectUnigram(await Chain.GetValuesForKey(key));
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
        private string[] PadArrayLow(string[]? input)
        {
            input ??= new List<string>().ToArray();

            var splitCount = input.Length;
            if (splitCount > Level)
            {
                input = [.. input.Skip(splitCount - Level).Take(Level)];
            }

            var p = new string[Level];
            var j = 0;
            for (var i = Level - input.Length; i < Level; i++)
            {
                p[i] = input[j];
                j++;
            }
            for (var i = Level - input.Length; i > 0; i--)
            {
                p[i - 1] = "";
            }

            return p;
        }
    }
}
